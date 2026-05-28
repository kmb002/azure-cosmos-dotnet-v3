namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class HybridSearchQueryTests : QueryTestsBase
    {
        private const string CollectionDataPath = "Documents\\text-3properties-1536dimensions-100documents.json";
        private const string CacheDiagnosticsQuery = @"
            SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
            FROM c
            WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
            ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))";
        private const string FullTextScoreStatsCacheStatusDatum = "BM25FullTextScoreStatsCacheStatus";
        private const string FullTextScoreStatsCacheHit = "Hit";
        private const string FullTextScoreStatsCacheMiss = "Miss";

        private const string SampleVector = @"[0.02, 0, -0.02, 0, -0.04, -0.01, -0.04, -0.01, 0.06, 0.08, -0.05, -0.04, -0.03, 0.05, -0.03, 0, -0.03, 0, 0.05, 0, 0.03,
0.02, 0, 0.04, 0.05, 0.03, 0, 0, 0, -0.03, -0.01, 0.01, 0, -0.01, -0.03, -0.02, -0.05, 0.01, 0, 0.01, 0, 0.01, -0.03, -0.02, 0.02, 0.02, 0.04, 0.01, 0.04, 0.02, -0.01, -0.01, 0.02, 0.01, 0.02, -0.04, -0.01, 0.06, -0.01, -0.03, -0.04, -0.01, -0.01, 0, 0.03,
-0.02, 0.03, 0.05, 0.01, 0.04, 0.05, -0.05, -0.01, 0.03, 0.02, -0.02, 0, -0.02, -0.02, -0.04, 0.01, -0.05, 0.01, 0.05, 0, -0.02, 0.03, -0.07, 0.05, 0.02, 0.03, 0.05, 0.05, -0.01, 0.03, -0.08, -0.01, -0.03, 0.04, -0.01, -0.02, -0.01, -0.02, -0.03, 0.03, 0.03,
-0.04, 0.04, 0.02, 0, 0.03, -0.02, -0.04, 0.02, 0.01, 0.02, -0.01, 0.03, 0.02, 0.01, -0.02, 0, 0.02, 0, -0.01, 0.02, -0.05, 0.03, 0.03, 0.04, -0.02, 0.04, -0.04, 0.03, 0.03, -0.03, 0, 0.02, 0.06, 0.02, 0.02, -0.01, 0.03, 0, -0.03, -0.06, 0.02, 0, 0.02, -0.04,
-0.05, 0.01, 0.02, 0.02, 0.07, 0.05, -0.01, 0.03, -0.03, -0.06, 0.04, 0.01, -0.01, 0.04, 0.02, 0.03, -0.03, 0.03, -0.01, 0.03, -0.04, -0.02, 0.02, -0.02, -0.03, -0.02, 0.02, -0.01, -0.05, -0.07, 0.02, -0.01, 0, -0.01, -0.02, -0.02, -0.03, -0.03, 0, -0.08, -0.01,
0, -0.01, -0.03, 0.01, 0, -0.02, -0.03, -0.04, -0.01, 0.02, 0, 0, -0.04, 0.04, -0.01, 0.04, 0, -0.06, 0.02, 0.03, 0.01, 0.06, -0.02, 0, 0.01, 0.01, 0.01, 0, -0.02, 0.03, 0.02, 0.01, -0.01, -0.05, 0.03, -0.04, 0, 0.01, -0.02, -0.04, 0.02, 0, 0.09, -0.04, -0.01,
0.02, 0.01, -0.03, 0.04, 0.02, -0.02, -0.02, -0.01, 0.01, -0.04, -0.01, 0.02, 0, 0, 0.07, 0.02, 0, 0, -0.01, 0.01, 0.03, -0.02, 0, 0.03, -0.02, -0.07, -0.04, -0.03, 0, -0.03, -0.02, 0, -0.02, -0.02, -0.05, -0.02, 0, 0.05, 0.01, -0.01, -0.04, 0.02, 0, 0, 0.03,
0.02, -0.03, -0.01, -0.02, 0.06, -0.02, 0.01, 0.01, 0.04, -0.04, 0.06, -0.02, 0.01, 0.03, 0.01, 0.02, -0.02, 0.01, -0.04, 0.05, -0.03, 0.01, -0.01, 0, -0.03, -0.03, 0.04, 0.02, -0.03, -0.03, -0.02, 0.06, 0.04, -0.01, 0.01, 0.01, -0.01, -0.02, -0.02, 0.04, 0.01,
-0.01, 0.01, -0.01, 0, 0.01, -0.04, 0.01, 0, -0.04, 0.05, 0.01, 0.01, 0.09, -0.04, -0.02, 0.04, 0, 0.04, -0.04, -0.04, 0, 0, -0.01, 0.05, -0.01, 0.02, 0.01, -0.03, 0, -0.06, 0.02, 0.04, 0.01, 0.03, 0.01, -0.04, 0, 0.01, 0.05, 0.02, -0.02, 0.02, 0, -0.02, -0.04,
-0.07, -0.02, -0.05, 0.06, 0.01, 0.02, -0.03, 0.06, -0.01, -0.02, -0.02, -0.01, 0, -0.05, 0.06, -0.05, 0, -0.02, -0.02, 0, -0.01, 0.01, 0, -0.01, 0.05, 0.02, 0, 0.02, -0.02, 0.02, 0, 0.08, -0.02, 0.01, -0.03, 0.02, -0.03, 0, -0.01, -0.02, -0.04, 0.06, 0.01,
-0.03, -0.03, 0.01, -0.01, 0.01, -0.01, 0.02, -0.03, 0.03, 0.04, 0.02, -0.02, 0.04, 0.01, 0.01, 0.02, 0.01, 0, -0.03, 0.03, -0.02, -0.03, -0.02, 0.02, 0, -0.01, -0.02, -0.02, 0, -0.01, -0.03, 0.02, -0.01, 0.01, -0.08, 0.01, -0.04, -0.05, 0.02, -0.01, -0.03,
0.02, 0.01, -0.03, 0.01, 0.02, 0.03, 0.04, -0.04, 0.02, 0, 0.02, 0.02, 0.04, -0.04, -0.1, 0, 0.05, -0.01, 0.03, 0.05, 0.03, -0.02, 0.01, 0.02, -0.05, 0.01, 0, 0.05, -0.01, 0.03, -0.01, 0, 0.04, 0, 0, 0.08, 0.01, 0, -0.04, -0.03, 0, -0.02, -0.01, 0.02, 0.03,
0, -0.01, 0, 0, 0, 0.06, 0, 0, 0.01, -0.01, 0.01, 0.04, 0.07, -0.01, 0.01, 0, -0.01, -0.02, 0.01, 0.01, 0, 0.02, 0.01, 0, -0.02, 0.03, 0.02, 0.06, 0.02, -0.01, 0.03, 0.02, -0.02, 0.01, -0.01, 0.03, 0.05, 0.02, 0.01, 0, 0, 0.01, 0.03, -0.03, -0.01, -0.04, 0.03,
-0.02, 0.02, -0.02, -0.01, -0.02, 0.01, -0.04, 0.01, -0.04, 0.03, -0.02, -0.02, -0.01, -0.01, 0.07, 0.04, -0.01, 0.08, -0.04, -0.04, 0, 0, -0.01, -0.01, 0.03, -0.04, 0.02, -0.01, -0.04, 0.02, -0.07, -0.02, 0.02, -0.01, 0.02, 0.01, 0, 0.07, -0.01, 0.03, 0.01,
-0.05, 0.02, 0.02, -0.01, 0.02, 0.02, -0.03, -0.02, 0.03, -0.01, 0.02, 0, 0, 0.02, -0.01, -0.02, 0.05, 0.02, 0.01, 0.01, -0.03, -0.05, -0.03, 0.01, 0.03, -0.02, -0.01, -0.01, -0.01, 0.03, -0.01, -0.03, 0.02, -0.02, -0.03, -0.02, -0.01, -0.01, -0.01, 0, -0.01,
-0.04, -0.02, -0.02, -0.03, 0.04, 0.03, 0, -0.02, -0.01, -0.03, -0.01, -0.04, -0.04, 0.02, 0.01, -0.05, 0.04, -0.03, 0.01, -0.01, -0.03, 0.01, 0.01, 0.01, 0.02, -0.01, -0.02, -0.03, -0.01, -0.01, -0.01, -0.01, -0.03, 0, 0.01, -0.02, -0.01, -0.01, 0.01, 0, -0.04,
0.01, -0.01, 0.02, 0, 0, -0.01, 0, 0, 0.03, -0.01, -0.06, -0.04, -0.01, 0, 0.02, -0.05, -0.02, 0.02, -0.01, 0.01, 0.01, -0.01, -0.02, 0, 0.02, -0.01, -0.02, 0.04, -0.01, 0, -0.02, -0.04, -0.03, -0.03, 0, 0.03, -0.01, -0.02, 0, 0.01, -0.01, -0.04, 0.01, -0.03,
0.01, 0.03, 0, -0.02, 0, -0.04, -0.02, -0.02, 0.03, -0.02, 0.05, 0.02, 0.03, -0.02, -0.05, -0.01, 0.02, -0.04, 0.02, 0.01, -0.03, 0.01, 0.02, 0, 0.04, 0, -0.01, 0.02, 0.01, 0.02, 0.02, -0.02, 0.04, -0.01, 0, -0.01, 0, 0.01, -0.02, -0.04, 0.06, 0.01, 0, 0.01,
-0.02, 0.02, 0.05, 0, 0.03, -0.02, 0.02, -0.03, -0.02, 0.01, 0, 0.06, -0.01, 0, -0.02, -0.02, 0.01, -0.01, 0, -0.03, 0.02, 0, -0.01, -0.02, -0.01, 0.03, -0.03, 0, 0, 0, -0.03, -0.06, 0.04, 0.02, -0.03, -0.06, -0.03, -0.01, -0.03, -0.02, -0.04, 0.01, 0, -0.01,
0.02, -0.01, 0.03, 0.02, -0.02, -0.01, -0.02, -0.03, -0.01, 0.01, -0.04, 0.04, 0.03, 0.02, 0, -0.07, -0.02, -0.01, 0, 0.03, -0.01, -0.03, 0, 0.03, 0, -0.01, 0.02, 0.01, 0.02, -0.03, 0, 0.01, -0.02, 0.04, -0.04, 0, -0.05, 0, -0.02, -0.01, 0.03, 0.01, 0, -0.02,
0, -0.05, 0.01, -0.01, 0, -0.08, -0.01, -0.02, 0.02, 0.01, -0.01, -0.01, -0.01, 0, 0, -0.01, -0.03, 0, 0, -0.02, 0.05, -0.03, 0.02, 0.01, -0.02, 0.01, 0.01, 0, 0.01, -0.01, 0, -0.04, -0.06, 0.03, -0.02, 0, -0.02, 0.01, 0.03, 0.03, -0.03, -0.01, 0, 0, 0.01,
-0.02, -0.01, -0.01, -0.03, -0.02, 0.03, -0.02, 0.03, 0.01, 0.04, -0.04, 0.02, 0.02, 0.02, 0.03, 0, 0.06, -0.01, 0.02, -0.01, 0.01, -0.01, -0.01, -0.03, -0.01, 0.02, 0.01, 0.01, 0, -0.02, 0.03, 0.02, -0.01, -0.02, 0.01, 0.01, 0.04, -0.01, -0.05, 0, -0.01, 0,
0.03, -0.01, 0.02, 0.02, -0.04, 0.01, -0.03, -0.02, 0, 0.02, 0, -0.01, 0.02, 0.01, 0.04, -0.04, 0, -0.01, -0.02, 0, -0.02, 0.01, -0.02, 0, 0, 0.03, 0.04, -0.01, 0, 0, 0.03, -0.02, 0.01, -0.02, 0, -0.03, 0.04, 0, 0.01, 0.04, 0, 0.03, -0.02, 0.01, 0.01, -0.02,
0.02, -0.05, 0.03, -0.02, -0.01, 0.01, -0.01, 0.02, 0.04, 0.02, 0, -0.02, 0.02, -0.01, -0.03, -0.06, -0.01, -0.01, -0.04, 0.01, -0.01, -0.01, -0.01, -0.02, 0.03, -0.03, 0.05, 0, -0.01, -0.03, 0.03, 0.01, -0.01, -0.01, 0, 0.01, 0.01, 0.02, -0.01, 0.02, -0.02,
-0.03, 0.03, -0.02, 0.01, 0, -0.03, 0.02, 0.02, -0.02, 0.01, 0.02, -0.01, 0.02, 0, 0.02, 0.01, 0, 0.05, -0.03, 0.01, 0.03, 0.04, 0.01, 0.01, -0.01, 0.02, -0.03, 0.02, 0.01, 0, -0.01, -0.03, -0.01, 0.02, 0.03, 0, 0.03, 0.02, 0, 0.01, 0.01, 0.02, 0.01, 0.02, 0.03,
0.01, -0.03, 0.02, 0.01, 0.02, 0.03, -0.01, 0.01, -0.03, -0.01, -0.02, 0.01, 0, 0, -0.01, -0.02, -0.01, -0.01, 0.01, 0.06, 0.01, 0, -0.01, 0.01, 0, 0, -0.01, -0.01, 0, -0.02, -0.02, -0.01, -0.02, -0.01, -0.05, -0.02, 0.03, 0.02, 0, 0.03, -0.03, -0.03, 0.03, 0,
0.02, -0.03, 0.04, -0.04, 0, -0.04, 0.04, 0.01, -0.03, 0.01, -0.02, -0.01, -0.04, 0.02, -0.01, 0.01, 0.01, 0.02, -0.02, 0.03, -0.01, 0, 0.01, 0, 0.02, 0.01, 0.01, 0.03, -0.06, 0.02, 0, -0.02, 0, 0.04, -0.03, 0, 0, -0.02, 0.06, 0.01, -0.03, -0.02, -0.01, -0.03,
-0.04, 0.04, 0.03, -0.02, 0, 0.03, -0.04, -0.01, -0.02, -0.02, -0.01, 0.02, 0.02, 0.01, 0.01, 0.01, -0.02, -0.02, -0.03, -0.01, 0.01, 0, 0, 0, 0.02, -0.04, -0.01, -0.01, 0.04, -0.01, 0.01, -0.01, 0.01, -0.03, 0.01, -0.01, 0, -0.01, 0.01, 0, 0.01, -0.04, 0.01, 0,
0, 0, 0, 0.02, 0.04, 0.01, 0.01, -0.01, -0.02, 0, 0, 0.01, -0.01, 0.01, -0.01, 0, 0.04, -0.01, -0.02, -0.01, -0.01, -0.01, 0, 0, 0.01, 0.01, 0.04, -0.01, -0.01, 0, -0.03, -0.01, 0.01, -0.01, -0.02, 0.01, -0.02, 0.01, -0.03, 0.02, 0, 0.03, 0.01, -0.03, -0.01,
-0.01, 0.02, 0.01, 0, -0.01, 0.03, -0.04, 0.01, -0.01, -0.03, -0.02, 0.02, -0.01, 0, -0.01, 0.02, 0.02, 0.01, 0.03, 0, -0.03, 0, 0.02, -0.03, -0.01, 0.01, 0.06, -0.01, -0.02, 0.01, 0, 0.04, -0.04, 0.01, -0.02, 0, -0.04, 0, 0.02, 0.02, -0.02, 0.04, -0.01, 0.01,
0, 0.03, -0.03, 0.04, -0.01, -0.02, -0.02, 0.01, -0.02, -0.01, 0, -0.03, -0.01, 0.02, -0.01, -0.05, 0.02, 0.01, 0, -0.02, -0.03, 0, 0, 0, -0.01, 0.02, 0, 0.02, 0.03, -0.02, 0.02, -0.02, 0.02, -0.01, 0.02, 0, -0.07, -0.01, 0.01, 0.01, -0.01, 0.02, 0, -0.01, 0,
0.01, 0.01, -0.06, 0.04, 0, -0.04, -0.01, -0.03, -0.04, -0.01, -0.01, 0.03, -0.02, -0.01, 0.02, 0, -0.04, 0.01, 0.01, -0.01, 0.02, 0.01, 0.03, -0.01, 0, -0.02, -0.02, -0.01, 0.04, -0.02, 0.06, 0, 0, -0.02, 0, 0.01, 0, -0.02, 0.02, 0.02, -0.06, -0.02, 0, 0.02,
0.01, -0.01, 0, 0, -0.01, 0.01, -0.04, -0.01, -0.01, 0.01, -0.02, -0.03, 0.01, 0.03, -0.01, -0.01, 0, -0.01, 0, -0.01, 0.05, 0.02, 0, 0, 0.02, -0.01, 0.02, -0.03, -0.01, -0.02, 0.02, 0, 0.01, -0.06, -0.01, 0.01, 0.01, 0.02, 0.02, -0.02, 0.03, 0.01, -0.01, -0.01,
0, 0, 0.03, 0.05, 0.05, -0.01, 0.01, -0.03, 0, -0.01, -0.01, 0, -0.02, 0.02, 0, 0.02, -0.01, 0.01, -0.02, 0.01, 0, -0.02, 0.02, 0.01, -0.03, 0.03, -0.04, -0.02, -0.01, 0.01, -0.04, -0.03, -0.02, -0.03, 0.01, 0, 0, -0.02, -0.01, 0.02, 0.01, -0.01, 0.01, 0.03,
-0.01, -0.02, -0.01, 0, 0, -0.03, 0, 0.02, 0.03, 0.01, -0.01, 0.02, 0.04, -0.04, 0.02, 0.01, -0.02, -0.01, 0.03, -0.04, -0.01, 0, 0.01, 0.01, 0, 0.03, 0.05, 0, 0, 0.05, 0.01, -0.01, 0, -0.01, 0, -0.01, -0.01, 0.03, -0.01, 0.02, 0, 0, -0.01, 0, -0.02, -0.02,
0.05, -0.02, -0.01, -0.01, -0.01, 0.02, 0, -0.01, 0, 0, 0, -0.02, -0.04, 0.01, 0.01, -0.01, 0.01, 0, -0.06, -0.01, -0.04, -0.03, 0.01, 0, -0.01, 0.03, -0.04, -0.01, 0, 0.04, 0.03]";

        private static readonly IndexingPolicy CompositeIndexPolicy = CreateIndexingPolicy();

        [TestMethod]
        public async Task SanityTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2, 57, 85 }, new List<int>{ 2, 85, 57 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE (FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')) AND (c.index = 2)
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2 } },
                    new PartitionKey(2)),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')",
                    new List<List<int>>{ new List<int>{ 2, 57, 85 }, new List<int>{ 2, 85, 57 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John')
                    ORDER BY RANK FullTextScore(c.title, 'John')
                    OFFSET 1 LIMIT 5",
                    new List<List<int>>{ new List<int>{ 57, 85 }, new List<int>{ 85, 57 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 57, 85 },
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 85, 57 },
                    }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{ new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 5 LIMIT 10",
                    new List<List<int>>{
                        new List<int>{ 24, 77, 76, 80, 2, 22, 57, 85 },
                        new List<int>{ 24, 77, 76, 80, 2, 22, 85, 57 },
                    }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))
                    OFFSET 0 LIMIT 11",
                    new List<List<int>>{ new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22 } }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), VectorDistance(c.vector, {SampleVector}))",
                    new List<List<int>>{new List<int>{ 21, 37, 75, 26, 35, 24, 87, 55, 49, 9 } }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{new List<int>{ 21, 37, 75, 26, 35, 24, 87, 55, 49, 9 } }),
                MakeSanityTest($@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    ORDER BY RANK RRF(VectorDistance(c.vector, {SampleVector}), FullTextScore(c.title, 'John'), VectorDistance(c.image, {SampleVector}), VectorDistance(c.backup_image, {SampleVector}), FullTextScore(c.text, 'United States'))",
                    new List<List<int>>{new List<int>{ 21, 37, 75, 26, 35, 24, 87, 55, 49, 9 } }),
            };

            await this.RunTests(testCases);
        }

        [TestMethod]
        public async Task WeightedRankFusionTests()
        {
            List<SanityTestCase> testCases = new List<SanityTestCase>
            {
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [1, 1])",
                    new List<List<int>>{
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 85, 57 },
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 57, 85 },
                    }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [10, 10])",
                    new List<List<int>>{
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 57, 85 },
                        new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2, 22, 85, 57 },
                    }),
                MakeSanityTest(@"
                    SELECT TOP 10 c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [0.1, 0.1])",
                    new List<List<int>>{ new List<int>{ 61, 51, 49, 54, 75, 24, 77, 76, 80, 2 } }),
                MakeSanityTest(@"
                    SELECT c.index AS Index, c.title AS Title, c.text AS Text
                    FROM c
                    WHERE FullTextContains(c.title, 'John') OR FullTextContains(c.text, 'John') OR FullTextContains(c.text, 'United States')
                    ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [-1, -1])",
                    new List<List<int>>{ new List<int>{ 57, 85, 2, 22, 80, 76, 77, 24, 75, 54, 49, 51, 61 } }),
            };

            await this.RunTests(testCases);
        }

        [TestMethod]
        public async Task FullTextScoreStatsCacheAddsHitAndMissDiagnostics()
        {
            CosmosArray documentsArray = await LoadDocuments();
            IEnumerable<string> documents = documentsArray.Select(document => document.ToString());
            string databaseId = this.database.Id;

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct,
                collectionTypes: CollectionTypes.MultiPartition,
                documents: documents,
                query: async (container, _) =>
                {
                    using CosmosClient cacheEnabledClient = SDK.EmulatorTests.TestCommon.CreateCosmosClient(
                        useGateway: false,
                        customizeClientBuilder: builder => builder.WithFullTextScoreStatsCacheTtl(TimeSpan.FromMinutes(5)));
                    Container cacheEnabledContainer = cacheEnabledClient.GetContainer(databaseId, container.Id);

                    QueryRequestOptions requestOptions = new QueryRequestOptions
                    {
                        FullTextScoreScope = FullTextScoreScope.Global,
                        MaxItemCount = 10,
                    };

                    (List<int> missIndices, List<string> missStatuses) = await ExecuteQueryAndCollectCacheStatusesAsync(
                        cacheEnabledContainer,
                        CacheDiagnosticsQuery,
                        requestOptions);
                    (List<int> hitIndices, List<string> hitStatuses) = await ExecuteQueryAndCollectCacheStatusesAsync(
                        cacheEnabledContainer,
                        CacheDiagnosticsQuery,
                        requestOptions);

                    CollectionAssert.AreEqual(missIndices, hitIndices);
                    Assert.IsTrue(missStatuses.Contains(FullTextScoreStatsCacheMiss), "Expected a cache miss diagnostic on the first global query.");
                    Assert.IsFalse(missStatuses.Contains(FullTextScoreStatsCacheHit), "Did not expect a cache hit diagnostic on the first global query.");
                    Assert.IsTrue(hitStatuses.Contains(FullTextScoreStatsCacheHit), "Expected a cache hit diagnostic on the second global query.");
                    Assert.IsFalse(hitStatuses.Contains(FullTextScoreStatsCacheMiss), "Did not expect a cache miss diagnostic on the second global query.");
                },
                partitionKey: "/index",
                indexingPolicy: CompositeIndexPolicy);
        }

        [TestMethod]
        public async Task FullTextScoreStatsCacheLocalScopeBypassesDiagnostics()
        {
            CosmosArray documentsArray = await LoadDocuments();
            IEnumerable<string> documents = documentsArray.Select(document => document.ToString());
            string databaseId = this.database.Id;

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct,
                collectionTypes: CollectionTypes.MultiPartition,
                documents: documents,
                query: async (container, _) =>
                {
                    using CosmosClient cacheEnabledClient = SDK.EmulatorTests.TestCommon.CreateCosmosClient(
                        useGateway: false,
                        customizeClientBuilder: builder => builder.WithFullTextScoreStatsCacheTtl(TimeSpan.FromMinutes(5)));
                    Container cacheEnabledContainer = cacheEnabledClient.GetContainer(databaseId, container.Id);

                    QueryRequestOptions requestOptions = new QueryRequestOptions
                    {
                        FullTextScoreScope = FullTextScoreScope.Local,
                        MaxItemCount = 10,
                    };

                    (List<int> firstIndices, List<string> firstStatuses) = await ExecuteQueryAndCollectCacheStatusesAsync(
                        cacheEnabledContainer,
                        CacheDiagnosticsQuery,
                        requestOptions);
                    (List<int> secondIndices, List<string> secondStatuses) = await ExecuteQueryAndCollectCacheStatusesAsync(
                        cacheEnabledContainer,
                        CacheDiagnosticsQuery,
                        requestOptions);

                    CollectionAssert.AreEqual(firstIndices, secondIndices);
                    Assert.AreEqual(0, firstStatuses.Count, "Did not expect cache diagnostics for local full text score scope.");
                    Assert.AreEqual(0, secondStatuses.Count, "Did not expect cache diagnostics for local full text score scope.");
                },
                partitionKey: "/index",
                indexingPolicy: CompositeIndexPolicy);
        }

        private async Task RunTests(IEnumerable<SanityTestCase> testCases)
        {
            CosmosArray documentsArray = await LoadDocuments();
            IEnumerable<string> documents = documentsArray.Select(document => document.ToString());

            await this.CreateIngestQueryDeleteAsync(
                connectionModes: ConnectionModes.Direct, // | ConnectionModes.Gateway,
                collectionTypes: CollectionTypes.MultiPartition, // | CollectionTypes.SinglePartition,
                documents: documents,
                query: (container, _) => RunTests(container, testCases),
                partitionKey: "/index",
                indexingPolicy: CompositeIndexPolicy);
        }

        private static async Task RunTests(Container container, IEnumerable<SanityTestCase> testCases)
        {
            foreach (FullTextScoreScope fullTextScoreScope in new[]{ FullTextScoreScope.Local, FullTextScoreScope.Global })
            {
                foreach (SanityTestCase testCase in testCases)
                {
                    QueryRequestOptions testRequestOptions = new QueryRequestOptions
                    {
                        FullTextScoreScope = fullTextScoreScope,
                    };

                    if (testCase.PartitionKey.HasValue)
                    {
                        testRequestOptions.PartitionKey = testCase.PartitionKey;
                    }

                    List<TextDocument> result = await RunQueryCombinationsAsync<TextDocument>(
                        container,
                        testCase.Query,
                        queryRequestOptions: testRequestOptions,
                        queryDrainingMode: QueryDrainingMode.HoldState);

                    IEnumerable<int> actual = result.Select(document => document.Index);

                    bool match = false;
                    foreach (IReadOnlyList<int> expectedIndices in testCase.ExpectedIndices)
                    {
                        if (expectedIndices.SequenceEqual(actual))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (!match)
                    {
                        Trace.WriteLine($"Query: {testCase.Query}");
                        Trace.WriteLine($"Actual: {string.Join(", ", actual)}");

                        string errorMessage = @"The query results did not match any of the expected results." +
                            "Please set HybridSearchCrossPartitionQueryPipelineStage.HybridSearchDebugTraceHelpers.Enabled = true to debug." +
                            "Usually, the failure may be due to some swaps in the results that have equal scores. You can see this in the debug output." +
                            "The solution is to add another expected result that matches the actual results (provided the scores are in decresing order).";
                        Assert.Fail(errorMessage);
                    }
                }
            }
        }

        private static async Task<(List<int> indices, List<string> cacheStatuses)> ExecuteQueryAndCollectCacheStatusesAsync(
            Container container,
            string query,
            QueryRequestOptions requestOptions)
        {
            List<int> indices = new List<int>();
            List<string> cacheStatuses = new List<string>();

            await foreach (FeedResponse<TextDocument> response in RunSimpleQueryAsync<TextDocument>(container, query, requestOptions))
            {
                indices.AddRange(response.Select(document => document.Index));

                if ((response.Diagnostics is CosmosTraceDiagnostics traceDiagnostics) &&
                    traceDiagnostics.Value.TryGetDatum(FullTextScoreStatsCacheStatusDatum, out object cacheStatus) &&
                    (cacheStatus is string cacheStatusText))
                {
                    cacheStatuses.Add(cacheStatusText);
                }
            }

            return (indices, cacheStatuses);
        }

        private static async Task<CosmosArray> LoadDocuments()
        {
            // read the json file
            string json = await File.ReadAllTextAsync(CollectionDataPath);
            byte[] jsonBuffer = Encoding.UTF8.GetBytes(json);
            ReadOnlyMemory<byte> readOnlyMemory = new ReadOnlyMemory<byte>(jsonBuffer);
            CosmosObject rootObject = CosmosObject.CreateFromBuffer(readOnlyMemory);
            Assert.IsTrue(rootObject.TryGetValue(FieldNames.Items, out CosmosArray items), "Failed to find items in the json file.");
            return items;
        }

        private static IndexingPolicy CreateIndexingPolicy()
        {
            IndexingPolicy policy = new IndexingPolicy();

            policy.IncludedPaths.Add(new IncludedPath { Path = IndexingPolicy.DefaultPath });
            policy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new CompositePath { Path = $"/index" },
                new CompositePath { Path = $"/mixedTypefield" },
            });

            return policy;
        }

        private static SanityTestCase MakeSanityTest(string query, IReadOnlyList<IReadOnlyList<int>> expectedIndices, PartitionKey? partitionKey = null)
        {
            return new SanityTestCase
            {
                Query = query,
                ExpectedIndices = expectedIndices,
                PartitionKey = partitionKey,
            };
        }

        private sealed class SanityTestCase
        {
            public string Query { get; init; }

            public IReadOnlyList<IReadOnlyList<int>> ExpectedIndices { get; init; }

            public PartitionKey? PartitionKey { get; init; }
        }

        private sealed class TextDocument
        {
            public int Index { get; set; }

            public string Title { get; set; }

            public string Text { get; set; }
        }

        private static class FieldNames
        {
            public const string Items = "items";
            public const string Index = "index";
            public const string Title = "title";
            public const string Text = "text";
            public const string Rid = "_rid";
        }
    }
}
