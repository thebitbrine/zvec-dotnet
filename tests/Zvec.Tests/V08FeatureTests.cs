namespace Zvec.Tests;

/// <summary>
/// Features bound for the zvec v0.8.1 upgrade: hybrid vector + keyword search,
/// grouped search, delete by filter, stats, full scan, read-only open, search ef.
/// </summary>
[Collection("Zvec")]
public class V08FeatureTests : IDisposable
{
    private readonly string _tempDir;

    public V08FeatureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_v08_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name) => Path.Combine(_tempDir, name);

    // (pk, vector, text, category, year)
    private static readonly (string Pk, float[] Vec, string Text, string Category, int Year)[] Corpus =
    [
        ("a1", [1f, 0f, 0f, 0f], "restore hangs after manifest load", "ops", 2024),
        ("a2", [0.9f, 0.1f, 0f, 0f], "checkpoint stalls on the nfs mount", "ops", 2025),
        ("a3", [0.8f, 0.2f, 0f, 0f], "supervisor restart after backup", "ops", 2026),
        ("b1", [0f, 1f, 0f, 0f], "lunch menu has pasta and salad", "food", 2024),
        ("b2", [0f, 0.9f, 0.1f, 0f], "the zebrafish recipe needs more salt", "food", 2025),
        ("c1", [0f, 0f, 1f, 0f], "quarterly revenue projections show growth", "finance", 2026),
    ];

    private ZvecCollection CreateCorpus(string name)
    {
        var col = ZvecCollection.CreateAndOpen(CollPath(name), schema =>
        {
            schema.AddVector("vec", 4, MetricType.Cosine);
            schema.AddText("text");
            schema.AddScalar("category", DataType.String);
            schema.AddScalar("year", DataType.Int32);
        });

        foreach (var (pk, vec, text, category, year) in Corpus)
        {
            using var doc = new ZvecDocument(pk);
            doc.SetVector("vec", vec);
            doc.SetString("text", text);
            doc.SetString("category", category);
            doc.SetInt32("year", year);
            col.Insert(doc);
        }

        return col;
    }

    // =========================================================================
    // Version
    // =========================================================================

    [Fact]
    public void NativeVersion_IsAtLeast_0_8()
    {
        // everything in this file needs the v0.8 C API
        Console.WriteLine($"zvec native version: {ZvecRuntime.Version}");
        Assert.True(ZvecRuntime.VersionAtLeast(0, 8, 0), ZvecRuntime.Version);
        Assert.False(ZvecRuntime.VersionAtLeast(99, 0, 0));
    }

    // =========================================================================
    // Hybrid: dense vector + keyword in one multi-query
    // =========================================================================

    [Fact]
    public void Hybrid_VectorPlusKeyword_Rrf_FindsBothSides()
    {
        using var col = CreateCorpus("hybrid_rrf");

        // vector points at the "ops" cluster, keyword only exists in a "food" doc
        using var query = new MultiQuery(topK: 6)
            .AddSubQuery("vec", new[] { 1f, 0f, 0f, 0f }, numCandidates: 3)
            .AddFtsSubQuery("text", "zebrafish", numCandidates: 3)
            .WithRrfReranker();

        var ids = col.Query(query).Select(r => r.Id).ToList();

        Assert.Contains("a1", ids); // from the vector side
        Assert.Contains("b2", ids); // from the keyword side only
    }

    [Fact]
    public void Hybrid_DocMatchingBothSides_RanksFirst()
    {
        using var col = CreateCorpus("hybrid_both");

        using var query = new MultiQuery(topK: 3)
            .AddSubQuery("vec", new[] { 1f, 0f, 0f, 0f }, numCandidates: 3)
            .AddFtsSubQuery("text", "manifest restore", numCandidates: 3)
            .WithRrfReranker();

        var results = col.Query(query);

        Assert.NotEmpty(results);
        Assert.Equal("a1", results[0].Id);
    }

    [Fact]
    public void Hybrid_WeightedReranker_Works()
    {
        using var col = CreateCorpus("hybrid_weighted");

        using var query = new MultiQuery(topK: 6)
            .AddSubQuery("vec", new[] { 1f, 0f, 0f, 0f }, numCandidates: 3)
            .AddFtsSubQuery("text", "zebrafish", numCandidates: 3)
            .WithWeightedReranker(0.3, 0.7);

        var ids = col.Query(query).Select(r => r.Id).ToList();

        Assert.Contains("b2", ids);
    }

    [Fact]
    public void Hybrid_WithFilter_RestrictsResults()
    {
        using var col = CreateCorpus("hybrid_filter");

        using var query = new MultiQuery(topK: 6)
            .AddSubQuery("vec", new[] { 1f, 0f, 0f, 0f }, numCandidates: 6)
            .AddFtsSubQuery("text", "zebrafish pasta restore", numCandidates: 6)
            .WithRrfReranker()
            .WithFilter("year >= 2025");

        var ids = col.Query(query).Select(r => r.Id).ToList();

        Assert.NotEmpty(ids);
        Assert.DoesNotContain("a1", ids); // 2024
        Assert.DoesNotContain("b1", ids); // 2024
    }

    [Fact]
    public void Hybrid_FtsExpressionAndOperator_Work()
    {
        using var col = CreateCorpus("hybrid_expr");

        // expression: both terms required, only a2 has them
        using var expr = new MultiQuery(topK: 6)
            .AddSubQuery("vec", new[] { 0f, 0f, 1f, 0f }, numCandidates: 1)
            .AddFtsSubQuery("text", "checkpoint AND nfs", numCandidates: 6, isExpression: true)
            .WithRrfReranker();
        var exprIds = col.Query(expr).Select(r => r.Id).ToList();
        Assert.Contains("a2", exprIds);
        Assert.DoesNotContain("b1", exprIds);

        // AND operator on a natural-language match: no doc has both words
        using var and = new MultiQuery(topK: 6)
            .AddSubQuery("vec", new[] { 0f, 0f, 1f, 0f }, numCandidates: 1)
            .AddFtsSubQuery("text", "nfs pasta", numCandidates: 6, defaultOperator: "AND")
            .WithRrfReranker();
        var andIds = col.Query(and).Select(r => r.Id).ToList();
        Assert.Equal(new[] { "c1" }, andIds); // only the vector side contributed
    }

    // =========================================================================
    // Grouped search
    // =========================================================================

    [Fact]
    public void GroupBy_ReturnsBestPerGroup()
    {
        using var col = CreateCorpus("groupby");

        using var query = GroupByQuery.For("vec", new[] { 1f, 0f, 0f, 0f }, "category",
            groupCount: 3, topKPerGroup: 2);
        var groups = col.Query(query);

        Assert.NotEmpty(groups);
        Assert.True(groups.Count <= 3);
        Assert.Equal(groups.Count, groups.Select(g => g.Value).Distinct().Count());

        // closest group to the query vector comes first
        Assert.Equal("ops", groups[0].Value);
        Assert.Equal("a1", groups[0].Results[0].Id);

        foreach (var g in groups)
        {
            Assert.InRange(g.Results.Count, 1, 2);
            // every doc really belongs to its group
            foreach (var r in g.Results)
                Assert.Equal(g.Value, Corpus.First(c => c.Pk == r.Id).Category);
        }
    }

    [Fact]
    public void GroupBy_WithFilter_DropsGroups()
    {
        using var col = CreateCorpus("groupby_filter");

        using var query = GroupByQuery.For("vec", new[] { 1f, 0f, 0f, 0f }, "category",
            groupCount: 3, topKPerGroup: 2).WithFilter("year = 2026");
        var groups = col.Query(query);

        var ids = groups.SelectMany(g => g.Results).Select(r => r.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "a3", "c1" }, ids);
    }

    // =========================================================================
    // Delete by filter, stats
    // =========================================================================

    [Fact]
    public void DeleteByFilter_RemovesMatchingDocs()
    {
        using var col = CreateCorpus("delfilter");
        Assert.Equal(6UL, col.GetStats().DocCount);

        col.DeleteByFilter("year = 2024");

        Assert.Equal(4UL, col.GetStats().DocCount);
        using var fetched = col.Fetch(new[] { "a1", "b1", "a2" });
        Assert.Equal(new[] { "a2" }, fetched.Docs.Select(d => d.Pk).ToArray());
    }

    [Fact]
    public void GetStats_ListsIndexes()
    {
        using var col = CreateCorpus("stats");
        col.Optimize();

        var stats = col.GetStats();
        Console.WriteLine("indexes: " + string.Join(", ", stats.Indexes.Select(i => $"{i.Name}={i.Completeness}")));

        Assert.Equal(6UL, stats.DocCount);
        Assert.Contains(stats.Indexes, i => i.Name == "vec");
        Assert.All(stats.Indexes, i => Assert.InRange(i.Completeness, 0f, 1f));
    }

    // =========================================================================
    // Full scan
    // =========================================================================

    [Fact]
    public void Scan_VisitsEveryDoc()
    {
        using var col = CreateCorpus("scan");

        var seen = new Dictionary<string, string?>();
        foreach (var doc in col.Scan())
            seen[doc.Pk] = doc.GetString("text");

        Assert.Equal(Corpus.Select(c => c.Pk).OrderBy(x => x), seen.Keys.OrderBy(x => x));
        Assert.Equal("restore hangs after manifest load", seen["a1"]);
    }

    [Fact]
    public void Scan_OutputFieldsAndVector_AreHonoured()
    {
        using var col = CreateCorpus("scan_fields");

        foreach (var doc in col.Scan(outputFields: new[] { "year" }))
        {
            Assert.NotNull(doc.GetInt32("year"));
            Assert.Null(doc.GetString("text"));
            Assert.Null(doc.GetVector("vec"));
        }

        var withVec = col.Scan(includeVector: true).Select(d => (d.Pk, Vec: d.GetVector("vec"))).ToList();
        Assert.Equal(6, withVec.Count);
        Assert.All(withVec, d => Assert.Equal(4, d.Vec!.Length));
    }

    [Fact]
    public void Scan_DocIsUnreadableOnceEnumerationMovesOn()
    {
        using var col = CreateCorpus("scan_stale");

        var kept = col.Scan().ToList(); // every doc has been released by now
        Assert.Equal(6, kept.Count);
        Assert.Throws<ObjectDisposedException>(() => kept[0].Pk);
    }

    [Fact]
    public void Scan_AbandonedHalfway_StillLetsCollectionClose()
    {
        var col = CreateCorpus("scan_break");

        foreach (var doc in col.Scan())
            break; // foreach disposes the enumerator, which closes the native iterator

        col.Optimize(); // refused by zvec while an iterator is open
        col.Dispose();  // would wait forever on a leaked iterator
    }

    // =========================================================================
    // Read-only open
    // =========================================================================

    [Fact]
    public void OpenReadOnly_CanQuery_CannotWrite()
    {
        string path = CollPath("readonly");
        using (var col = CreateCorpus("readonly"))
            col.Flush();

        using var ro = ZvecCollection.Open(path, readOnly: true);

        using var query = VectorQuery.For("vec", new[] { 1f, 0f, 0f, 0f }, topK: 2);
        Assert.Equal("a1", ro.Query(query)[0].Id);

        using var kw = VectorQuery.ForFts("text", 3).WithFtsMatch("zebrafish");
        Assert.Equal("b2", ro.Query(kw)[0].Id);

        using var doc = new ZvecDocument("nope");
        doc.SetVector("vec", new[] { 1f, 0f, 0f, 0f });
        doc.SetString("text", "x");
        doc.SetString("category", "x");
        doc.SetInt32("year", 1);
        Assert.Throws<ZvecException>(() => ro.Insert(doc));
    }

    // =========================================================================
    // Search-time ef
    // =========================================================================

    [Fact]
    public void WithEf_StillReturnsNearestNeighbour()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("ef"), schema =>
        {
            schema.AddVector("vec", 16, MetricType.Cosine);
        });

        var rng = new Random(7);
        var vectors = new List<float[]>();
        for (int i = 0; i < 500; i++)
        {
            var v = new float[16];
            for (int j = 0; j < 16; j++) v[j] = (float)(rng.NextDouble() * 2 - 1);
            vectors.Add(v);

            using var doc = new ZvecDocument($"d{i}");
            doc.SetVector("vec", v);
            col.Insert(doc);
        }
        col.Optimize();

        using var query = VectorQuery.For("vec", vectors[123], topK: 5).WithEf(200);
        var results = col.Query(query);

        Assert.Equal(5, results.Count);
        Assert.Equal("d123", results[0].Id);
    }
}
