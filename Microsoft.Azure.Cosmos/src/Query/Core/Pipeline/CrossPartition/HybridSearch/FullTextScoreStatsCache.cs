// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class FullTextScoreStatsCache
    {
        private const int MaxEntryCount = 100;

        private readonly ConcurrentDictionary<string, CacheEntry> cache;
        private readonly Cosmos.CosmosSerializerCore serializerCore;
        private readonly object syncLock;

        public FullTextScoreStatsCache(TimeSpan timeToLive, Cosmos.CosmosSerializerCore serializerCore)
        {
            this.TimeToLive = timeToLive;
            this.serializerCore = serializerCore ?? throw new ArgumentNullException(nameof(serializerCore));
            this.cache = new ConcurrentDictionary<string, CacheEntry>();
            this.syncLock = new object();
        }

        public TimeSpan TimeToLive { get; }

        public string CreateCacheKey(
            string databaseId,
            string containerId,
            string globalStatisticsQueryText,
            SqlParameterCollection parameters)
        {
            if (databaseId == null)
            {
                throw new ArgumentNullException(nameof(databaseId));
            }

            if (containerId == null)
            {
                throw new ArgumentNullException(nameof(containerId));
            }

            if (globalStatisticsQueryText == null)
            {
                throw new ArgumentNullException(nameof(globalStatisticsQueryText));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            SqlQuerySpec querySpec = new SqlQuerySpec(globalStatisticsQueryText, parameters);

            byte[] querySpecBytes;
            using (Stream querySpecStream = this.serializerCore.ToStreamSqlQuerySpec(querySpec, ResourceType.Document))
            using (MemoryStream memoryStream = new MemoryStream())
            {
                querySpecStream.CopyTo(memoryStream);
                querySpecBytes = memoryStream.ToArray();
            }

            JObject payload = new JObject
            {
                // databaseId and containerId are necessary to disambiguate a cache entry within a CosmosClient
                // querying multiple containers (containerId is scoped to a database)
                ["databaseId"] = databaseId,
                ["containerId"] = containerId,
                ["querySpecBase64"] = Convert.ToBase64String(querySpecBytes),
            };

            byte[] payloadEncoded = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
            using (SHA256 sha256 = SHA256.Create())
            {
                // use SHA256 to create a compact key
                return BytesToHexString(sha256.ComputeHash(payloadEncoded));
            }
        }

        public bool TryGet(string cacheKey, out GlobalFullTextSearchStatistics statistics)
        {
            if (string.IsNullOrEmpty(cacheKey))
            {
                statistics = null;
                return false;
            }

            if (this.cache.TryGetValue(cacheKey, out CacheEntry cacheEntry))
            {
                if ((DateTime.UtcNow - cacheEntry.CachedAtUtc) <= this.TimeToLive)
                {
                    statistics = cacheEntry.Statistics;
                    return true;
                }

                this.cache.TryRemove(cacheKey, out _);
            }

            statistics = null;
            return false;
        }

        public void Set(string cacheKey, GlobalFullTextSearchStatistics statistics)
        {
            if (cacheKey == null)
            {
                throw new ArgumentNullException(nameof(cacheKey));
            }

            if (statistics == null)
            {
                throw new ArgumentNullException(nameof(statistics));
            }

            DateTime now = DateTime.UtcNow;
            lock (this.syncLock)
            {
                this.RemoveExpiredEntries(now);

                if (!this.cache.ContainsKey(cacheKey))
                {
                    this.EvictEntriesUntilBelowCapacity();
                }

                this.cache[cacheKey] = new CacheEntry(now, statistics);
            }
        }

        private void RemoveExpiredEntries(DateTime now)
        {
            foreach (KeyValuePair<string, CacheEntry> entry in this.cache)
            {
                if ((now - entry.Value.CachedAtUtc) > this.TimeToLive)
                {
                    this.cache.TryRemove(entry.Key, out _);
                }
            }
        }

        private void EvictEntriesUntilBelowCapacity()
        {
            while (this.cache.Count >= MaxEntryCount)
            {
                string oldestKey = null;
                DateTime oldestCachedAtUtc = DateTime.MaxValue;

                foreach (KeyValuePair<string, CacheEntry> entry in this.cache)
                {
                    if (entry.Value.CachedAtUtc < oldestCachedAtUtc)
                    {
                        oldestCachedAtUtc = entry.Value.CachedAtUtc;
                        oldestKey = entry.Key;
                    }
                }

                if (oldestKey == null)
                {
                    return;
                }

                this.cache.TryRemove(oldestKey, out _);
            }
        }

        private static string BytesToHexString(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        private sealed class CacheEntry
        {
            public CacheEntry(DateTime cachedAtUtc, GlobalFullTextSearchStatistics statistics)
            {
                this.CachedAtUtc = cachedAtUtc;
                this.Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
            }

            public DateTime CachedAtUtc { get; }

            public GlobalFullTextSearchStatistics Statistics { get; }
        }
    }
}
