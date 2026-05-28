// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using System.Collections.Concurrent;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Newtonsoft.Json;

    internal sealed class FullTextScoreStatsCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> cache;

        public FullTextScoreStatsCache(TimeSpan? timeToLive)
        {
            this.TimeToLive = timeToLive;
            this.cache = new ConcurrentDictionary<string, CacheEntry>();
        }

        public TimeSpan? TimeToLive { get; }

        public bool IsEnabled => this.TimeToLive.HasValue;

        public static string CreateCacheKey(
            string databaseId,
            string containerId,
            string globalStatisticsQueryText,
            SqlParameterCollection parameters)
        {
            if (string.IsNullOrEmpty(databaseId))
            {
                throw new ArgumentNullException(nameof(databaseId));
            }

            if (string.IsNullOrEmpty(containerId))
            {
                throw new ArgumentNullException(nameof(containerId));
            }

            if (string.IsNullOrEmpty(globalStatisticsQueryText))
            {
                throw new ArgumentNullException(nameof(globalStatisticsQueryText));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            string statisticsRequestHash = ComputeStatisticsRequestHash(globalStatisticsQueryText, parameters);

            return string.Concat(
                FormatKeySegment(databaseId), "|",
                FormatKeySegment(containerId), "|",
                statisticsRequestHash);
        }

        public bool TryGet(string cacheKey, out GlobalFullTextSearchStatistics statistics)
        {
            if (!this.IsEnabled || string.IsNullOrEmpty(cacheKey))
            {
                statistics = null;
                return false;
            }

            if (this.cache.TryGetValue(cacheKey, out CacheEntry cacheEntry))
            {
                if ((DateTime.UtcNow - cacheEntry.CachedAtUtc) <= this.TimeToLive.Value)
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
            if (!this.IsEnabled || string.IsNullOrEmpty(cacheKey) || statistics == null)
            {
                return;
            }

            this.cache[cacheKey] = new CacheEntry(DateTime.UtcNow, statistics);
        }

        private static string ComputeStatisticsRequestHash(
            string globalStatisticsQueryText,
            SqlParameterCollection parameters)
        {
            StringBuilder keyPayload = new StringBuilder();
            keyPayload.Append(FormatKeySegment(globalStatisticsQueryText));
            keyPayload.Append('|');
            keyPayload.Append(parameters.Count);

            foreach (SqlParameter parameter in parameters)
            {
                keyPayload.Append('|');
                keyPayload.Append(FormatKeySegment(parameter?.Name));
                keyPayload.Append('|');
                keyPayload.Append(FormatKeySegment(JsonConvert.SerializeObject(parameter?.Value, Formatting.None)));
            }

            byte[] payload = Encoding.UTF8.GetBytes(keyPayload.ToString());
            using (SHA256 sha256 = SHA256.Create())
            {
                return BytesToHexString(sha256.ComputeHash(payload));
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

        private static string FormatKeySegment(string value)
        {
            string normalizedValue = value ?? string.Empty;
            return $"{normalizedValue.Length}:{normalizedValue}";
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

    internal sealed class FullTextScoreStatsCacheContext
    {
        public FullTextScoreStatsCacheContext(
            FullTextScoreStatsCache cache,
            string databaseId,
            string containerId)
        {
            this.Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.DatabaseId = string.IsNullOrEmpty(databaseId) ? throw new ArgumentNullException(nameof(databaseId)) : databaseId;
            this.ContainerId = string.IsNullOrEmpty(containerId) ? throw new ArgumentNullException(nameof(containerId)) : containerId;
        }

        public FullTextScoreStatsCache Cache { get; }

        public string DatabaseId { get; }

        public string ContainerId { get; }
    }
}
