namespace Zvec.Tests;

[Collection("Zvec")]
public class SearchTests : IDisposable
{
    private readonly string _tempDir;

    public SearchTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_search_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name) => Path.Combine(_tempDir, name);

    private static float[] MakeVector(int dim, float seed)
    {
        var v = new float[dim];
        for (int i = 0; i < dim; i++)
            v[i] = seed + i * 0.01f;
        return v;
    }

    private static float[] NormalizeVector(float[] v)
    {
        float norm = 0;
        for (int i = 0; i < v.Length; i++)
            norm += v[i] * v[i];
        norm = MathF.Sqrt(norm);
        var result = new float[v.Length];
        for (int i = 0; i < v.Length; i++)
            result[i] = v[i] / norm;
        return result;
    }

    // =========================================================================
    // Basic query
    // =========================================================================

    [Fact]
    public void Query_AfterInsertAndIndex_ReturnsResults()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_basic"), schema =>
        {
            schema.AddVector("vec", 16, MetricType.Cosine);
        });

        // Insert 10 docs
        for (int i = 0; i < 10; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", NormalizeVector(MakeVector(16, i * 0.5f)));
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        using var query = VectorQuery.For("vec", NormalizeVector(MakeVector(16, 0.0f)), 5);
        var results = col.Query(query);

        Assert.NotEmpty(results);
        Assert.True(results.Count <= 5);
    }

    [Fact]
    public void Query_TopK5_ReturnsExactly5()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_topk5"), schema =>
        {
            schema.AddVector("vec", 16, MetricType.Cosine);
        });

        for (int i = 0; i < 20; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", NormalizeVector(MakeVector(16, i * 0.3f)));
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        using var query = VectorQuery.For("vec", NormalizeVector(MakeVector(16, 1.0f)), 5);
        var results = col.Query(query);

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_TopK1_ReturnsExactly1()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_topk1"), schema =>
        {
            schema.AddVector("vec", 16, MetricType.Cosine);
        });

        for (int i = 0; i < 10; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", NormalizeVector(MakeVector(16, i * 0.5f)));
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        using var query = VectorQuery.For("vec", NormalizeVector(MakeVector(16, 0.0f)), 1);
        var results = col.Query(query);

        Assert.Single(results);
    }

    [Fact]
    public void Query_TopKGreaterThanDocs_ReturnsAllDocs()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_topk_over"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
        });

        for (int i = 0; i < 5; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", NormalizeVector(MakeVector(8, i * 0.5f)));
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        using var query = VectorQuery.For("vec", NormalizeVector(MakeVector(8, 0.0f)), 100);
        var results = col.Query(query);

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_ResultsHaveValidPKAndScore()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_valid"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
        });

        for (int i = 0; i < 5; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", NormalizeVector(MakeVector(8, i * 0.5f)));
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        using var query = VectorQuery.For("vec", NormalizeVector(MakeVector(8, 0.0f)), 5);
        var results = col.Query(query);

        foreach (var r in results)
        {
            Assert.NotNull(r.Id);
            Assert.NotEmpty(r.Id);
            Assert.StartsWith("doc_", r.Id);
            // Score should be a valid number
            Assert.False(float.IsNaN(r.Score));
        }
    }

    [Fact]
    public void Query_IdenticalVector_Cosine_ReturnsCorrectDoc()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_identical"), schema =>
        {
            schema.AddVector("vec", 32, MetricType.Cosine);
        });

        // Use unnormalized vectors with enough variance
        var random = new Random(42);
        var targetVec = new float[32];
        for (int i = 0; i < 32; i++) targetVec[i] = (float)(random.NextDouble() * 2 - 1);

        using (var doc = new ZvecDocument("target"))
        {
            doc.SetVector("vec", targetVec);
            col.Insert(doc);
        }

        for (int i = 0; i < 10; i++)
        {
            var otherVec = new float[32];
            for (int j = 0; j < 32; j++) otherVec[j] = (float)(random.NextDouble() * 2 - 1);
            using var doc = new ZvecDocument($"other_{i}");
            doc.SetVector("vec", otherVec);
            col.Insert(doc);
        }

        col.CreateIndex("vec");
        col.Flush();

        using var query = VectorQuery.For("vec", targetVec, 1);
        var results = col.Query(query);

        Assert.Single(results);
        Assert.Equal("target", results[0].Id);
        // zvec returns cosine DISTANCE (not similarity). Identical vectors = distance 0.0.
        Assert.True(results[0].Score < 0.01f, $"Expected distance near 0.0 for identical vector, got {results[0].Score}");
    }

    [Fact]
    public void Query_WithL2Metric_Works()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_l2"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.L2);
        });

        var targetVec = new float[] { 1, 0, 0, 0, 0, 0, 0, 0 };

        using (var doc = new ZvecDocument("target"))
        {
            doc.SetVector("vec", targetVec);
            col.Insert(doc);
        }

        for (int i = 0; i < 5; i++)
        {
            using var doc = new ZvecDocument($"other_{i}");
            doc.SetVector("vec", MakeVector(8, (i + 5) * 1.0f));
            col.Insert(doc);
        }

        col.CreateIndex("vec", IndexType.Hnsw, MetricType.L2);

        using var query = VectorQuery.For("vec", targetVec, 1);
        var results = col.Query(query);

        Assert.Single(results);
        Assert.Equal("target", results[0].Id);
        // L2 distance of identical vectors should be very close to 0.0
        Assert.True(results[0].Score < 0.01f, $"Expected L2 score near 0.0, got {results[0].Score}");
    }

    [Fact]
    public void Query_100Docs_ReturnsCorrectCount()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_100"), schema =>
        {
            schema.AddVector("vec", 32, MetricType.Cosine);
        });

        for (int i = 0; i < 100; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", NormalizeVector(MakeVector(32, i * 0.1f)));
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        using var query = VectorQuery.For("vec", NormalizeVector(MakeVector(32, 0.0f)), 10);
        var results = col.Query(query);

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public void Query_MultipleQueriesSameCollection_AllWork()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_multi"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
        });

        for (int i = 0; i < 20; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", NormalizeVector(MakeVector(8, i * 0.3f)));
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        for (int q = 0; q < 5; q++)
        {
            using var query = VectorQuery.For("vec", NormalizeVector(MakeVector(8, q * 1.0f)), 3);
            var results = col.Query(query);
            Assert.Equal(3, results.Count);
        }
    }

    [Fact]
    public void Query_WithFlatIndex_Works()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_flat"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine, IndexType.Flat);
        });

        for (int i = 0; i < 10; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", NormalizeVector(MakeVector(8, i * 0.5f)));
            col.Insert(doc);
        }

        col.CreateIndex("vec", IndexType.Flat, MetricType.Cosine);

        using var query = VectorQuery.For("vec", NormalizeVector(MakeVector(8, 0.0f)), 5);
        var results = col.Query(query);

        Assert.True(results.Count > 0);
        Assert.True(results.Count <= 5);
    }

    [Fact]
    public void Query_EmptyCollection_ReturnsEmpty()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("q_empty"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
        });

        col.CreateIndex("vec");

        using var query = VectorQuery.For("vec", NormalizeVector(MakeVector(8, 0.0f)), 5);
        var results = col.Query(query);

        Assert.Empty(results);
    }
}
