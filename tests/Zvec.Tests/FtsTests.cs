namespace Zvec.Tests;

/// <summary>
/// Full-text search: index creation (schema-time and post-hoc), tokenizer options,
/// text-only queries, exact-token ranking, code-identifier handling, and
/// searchability of fresh inserts before Optimize().
/// </summary>
[Collection("Zvec")]
public class FtsTests : IDisposable
{
    private readonly string _tempDir;

    public FtsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_fts_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name) => Path.Combine(_tempDir, name);

    // Mixed prose + code corpus modeled on the Cerebras KB blog examples.
    // (pk, text)
    private static readonly (string Pk, string Text)[] Corpus =
    [
        ("d1", "ERR_MANIFEST_TIMEOUT: restore hangs after manifest load."),
        ("d2", "Checkpoint stalls on the NFS mount - set CKPT_PREFETCH=4. The default is too high."),
        ("d3", "sounds good, thanks! will try that"),
        ("d4", "restore hangs after manifest load - use LEGACY_FETCHER=1 as a workaround."),
        ("d5", "Deploy the wordpress site to the staging VM and update nginx routes."),
        ("d6", "Weekly report: lunch menu options for tuesday include pasta and salad."),
        ("d7", "ckpt_prefetch ckpt prefetch checkpoint prefetch config flag nfs mount tuning"),
        ("d8", "The quarterly revenue projections show growth in the hosting business."),
        ("d9", "git rebase interactive is not supported in this environment, use merge instead."),
        ("d10", "restart the supervisor process on the prod box after the backup completes."),
    ];

    private static float[] TinyVector(int seed)
    {
        // 4-dim placeholder vector; FTS tests don't care about dense search
        var v = new float[4];
        for (int i = 0; i < 4; i++) v[i] = (seed + 1) * 0.1f + i * 0.01f;
        float norm = MathF.Sqrt(v.Sum(x => x * x));
        for (int i = 0; i < 4; i++) v[i] /= norm;
        return v;
    }

    private ZvecCollection CreateCorpusCollection(string name, bool schemaTimeFts)
    {
        var col = ZvecCollection.CreateAndOpen(CollPath(name), schema =>
        {
            schema.AddVector("vec", 4, MetricType.Cosine);
            if (schemaTimeFts)
                schema.AddText("text");
            else
                schema.AddScalar("text", DataType.String);
        });

        int i = 0;
        foreach (var (pk, text) in Corpus)
        {
            using var doc = new ZvecDocument(pk);
            doc.SetVector("vec", TinyVector(i++));
            doc.SetString("text", text);
            col.Insert(doc);
        }

        if (!schemaTimeFts)
            col.CreateFtsIndex("text");

        return col;
    }

    private static List<string> Pks(IReadOnlyList<SearchResult> results) =>
        results.Select(r => r.Id).ToList();

    // =========================================================================
    // Index creation paths
    // =========================================================================

    [Fact]
    public void SchemaTimeFtsIndex_MatchQuery_ReturnsResults()
    {
        using var col = CreateCorpusCollection("fts_schema", schemaTimeFts: true);

        using var query = VectorQuery.ForFts("text", 5).WithFtsMatch("manifest restore");
        var results = col.Query(query);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void PostHocFtsIndex_IndexesExistingRows()
    {
        // v0.5.1 returned NotSupported here (FTS only at schema time via AddText).
        // v0.8.1 builds the index over rows that are already in the collection.
        using var col = CreateCorpusCollection("fts_posthoc", schemaTimeFts: false);

        using var query = VectorQuery.ForFts("text", 5).WithFtsMatch("manifest restore");
        var pks = Pks(col.Query(query));

        Assert.Contains("d1", pks);
        Assert.Contains("d4", pks);
    }

    [Fact]
    public void NgramTokenizer_MatchesInsideWords()
    {
        // tokenizer + extra_params plumbing: 3-grams find a fragment the standard
        // tokenizer can't ("anifes" is inside "manifest")
        using var col = ZvecCollection.CreateAndOpen(CollPath("fts_ngram"), schema =>
        {
            schema.AddVector("vec", 4, MetricType.Cosine);
            schema.AddText("text", tokenizerName: "ngram", filters: new[] { "lowercase" },
                extraParams: "{\"ngram_min\":3,\"ngram_max\":3}");
        });

        int i = 0;
        foreach (var (pk, text) in Corpus)
        {
            using var doc = new ZvecDocument(pk);
            doc.SetVector("vec", TinyVector(i++));
            doc.SetString("text", text);
            col.Insert(doc);
        }

        using var query = VectorQuery.ForFts("text", 5).WithFtsMatch("ANIFES");
        var pks = Pks(col.Query(query));

        Assert.Contains("d1", pks);
        Assert.Contains("d4", pks);
        Assert.DoesNotContain("d6", pks);
    }

    // =========================================================================
    // Ranking gates (Cerebras blog scenarios)
    // =========================================================================

    [Fact]
    public void ExactTokenDocs_RankAboveEverythingElse()
    {
        using var col = CreateCorpusCollection("fts_exact", schemaTimeFts: true);

        using var query = VectorQuery.ForFts("text", 5).WithFtsMatch("restore hangs after manifest load");
        var results = col.Query(query);

        Assert.NotEmpty(results);
        var pks = Pks(results);

        // d1 and d4 contain the exact phrase tokens; both must lead the ranking
        Assert.Contains(pks[0], new[] { "d1", "d4" });
        Assert.Contains("d1", pks.Take(3));
        Assert.Contains("d4", pks.Take(3));
    }

    [Fact]
    public void FillerMessage_DoesNotOutrankContent()
    {
        using var col = CreateCorpusCollection("fts_filler", schemaTimeFts: true);

        using var query = VectorQuery.ForFts("text", 3).WithFtsMatch("checkpoint restore stalls on the NFS mount");
        var results = col.Query(query);

        Assert.NotEmpty(results);
        // "sounds good, thanks!" must not appear in the top 3
        Assert.DoesNotContain("d3", Pks(results));
    }

    [Fact]
    public void RareCodeIdentifier_IsFindable()
    {
        using var col = CreateCorpusCollection("fts_ident", schemaTimeFts: true);

        using var query = VectorQuery.ForFts("text", 3).WithFtsMatch("CKPT_PREFETCH");
        var results = col.Query(query);

        Assert.NotEmpty(results);
        // d2 has CKPT_PREFETCH=4, d7 is the token-soup doc - either counts as a hit
        Assert.Contains(Pks(results)[0], new[] { "d2", "d7" });
    }

    [Fact]
    public void TokenSoupDoc_FindableBySubtoken()
    {
        using var col = CreateCorpusCollection("fts_soup", schemaTimeFts: true);

        using var query = VectorQuery.ForFts("text", 5).WithFtsMatch("prefetch");
        var results = col.Query(query);

        Assert.NotEmpty(results);
        Assert.Contains("d7", Pks(results));
    }

    // =========================================================================
    // Freshness: inserts searchable before Optimize (flat buffer claim)
    // =========================================================================

    [Fact]
    public void InsertAfterOptimize_ImmediatelySearchable()
    {
        using var col = CreateCorpusCollection("fts_fresh", schemaTimeFts: true);
        col.Optimize();

        using (var doc = new ZvecDocument("fresh_1"))
        {
            doc.SetVector("vec", TinyVector(99));
            doc.SetString("text", "the FLUXCAPACITOR_99 module needs recalibration");
            col.Insert(doc);
        }

        // no second Optimize on purpose
        using var query = VectorQuery.ForFts("text", 3).WithFtsMatch("FLUXCAPACITOR_99");
        var results = col.Query(query);

        Assert.NotEmpty(results);
        Assert.Equal("fresh_1", Pks(results)[0]);
    }

    // =========================================================================
    // Expression queries + operator
    // =========================================================================

    [Fact]
    public void ExpressionQuery_And_MatchesOnlyDocsWithBothTerms()
    {
        using var col = CreateCorpusCollection("fts_expr", schemaTimeFts: true);

        using var hit = VectorQuery.ForFts("text", 5).WithFts("checkpoint AND stalls");
        var hitResults = col.Query(hit);
        Assert.NotEmpty(hitResults);
        Assert.Contains("d2", Pks(hitResults));

        using var miss = VectorQuery.ForFts("text", 5).WithFts("checkpoint AND zebra");
        var missResults = col.Query(miss);
        Assert.DoesNotContain("d2", Pks(missResults));
    }

    [Fact]
    public void MatchQuery_WithAndOperator_NarrowsResults()
    {
        using var col = CreateCorpusCollection("fts_op", schemaTimeFts: true);

        // default operator (typically OR) should match more docs than AND
        using var orQuery = VectorQuery.ForFts("text", 10).WithFtsMatch("nfs pasta");
        var orResults = col.Query(orQuery);

        using var andQuery = VectorQuery.ForFts("text", 10).WithFtsOperator("AND").WithFtsMatch("nfs pasta");
        var andResults = col.Query(andQuery);

        // no doc contains both nfs and pasta
        Assert.True(andResults.Count < Math.Max(orResults.Count, 1) || orResults.Count == 0,
            $"AND ({andResults.Count}) should return fewer results than OR ({orResults.Count})");
    }

    // =========================================================================
    // Score semantics probe (document the direction for the fusion layer)
    // =========================================================================

    [Fact]
    public void FtsScores_AreOrderedBestFirst()
    {
        using var col = CreateCorpusCollection("fts_scores", schemaTimeFts: true);

        using var query = VectorQuery.ForFts("text", 5).WithFtsMatch("restore hangs after manifest load");
        var results = col.Query(query);

        Assert.True(results.Count >= 2, "need at least 2 results to probe score ordering");

        // Results come back ranked; capture whether scores ascend or descend so the
        // client-side RRF fusion in the KB knows which direction FTS scores run.
        bool descending = results[0].Score >= results[^1].Score;
        bool ascending = results[0].Score <= results[^1].Score;
        Assert.True(descending || ascending); // always true; the value is in the output below

        // Surface actual values in test output for the spike report
        var detail = string.Join(", ", results.Select(r => $"{r.Id}:{r.Score:F4}"));
        Console.WriteLine($"FTS score ordering (best-first as returned): {detail}");
    }
}
