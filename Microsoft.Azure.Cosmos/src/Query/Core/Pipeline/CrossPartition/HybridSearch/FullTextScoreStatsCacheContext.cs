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
