namespace Zvec.Tests;

[Collection("Zvec")]
public class HybridSearchTests : IDisposable
{
    private readonly string _tempDir;

    public HybridSearchTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_hybrid_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name) => Path.Combine(_tempDir, name);

    private static float[] RandomVector(int dim, Random rng)
    {
        var v = new float[dim];
        for (int i = 0; i < dim; i++)
            v[i] = (float)(rng.NextDouble() * 2 - 1);
        return v;
    }

    // =========================================================================
    // Multi-query with single dense field
    // =========================================================================

    [Fact]
    public void MultiQuery_SingleField_ThrowsRequiresTwo()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("mq_single"), schema =>
        {
            schema.AddVector("vec", 16, MetricType.Cosine);
        });

        var rng = new Random(42);
        for (int i = 0; i < 50; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", RandomVector(16, rng));
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        // MultiQuery requires at least 2 sub-queries
        using var mq = new MultiQuery(topK: 5);
        mq.AddSubQuery("vec", RandomVector(16, new Random(99)));

        var ex = Assert.Throws<ZvecException>(() => col.Query(mq));
        Assert.Contains("2 sub-queries", ex.Message);
    }

    // =========================================================================
    // Multi-query with two dense fields + RRF
    // =========================================================================

    [Fact]
    public void MultiQuery_TwoDenseFields_WithRrf_ReturnsResults()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("mq_rrf"), schema =>
        {
            schema.AddVector("title_vec", 16, MetricType.Cosine);
            schema.AddVector("content_vec", 16, MetricType.Cosine);
            schema.AddScalar("title", DataType.String);
        });

        var rng = new Random(42);
        for (int i = 0; i < 50; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("title_vec", RandomVector(16, rng));
            doc.SetVector("content_vec", RandomVector(16, rng));
            doc.SetString("title", $"Document {i}");
            col.Insert(doc);
        }

        col.CreateIndex("title_vec");
        col.CreateIndex("content_vec");

        using var mq = new MultiQuery(topK: 5);
        mq.AddSubQuery("title_vec", RandomVector(16, new Random(99)))
          .AddSubQuery("content_vec", RandomVector(16, new Random(100)))
          .WithRrfReranker(rankConstant: 60);

        var results = col.Query(mq);
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 5);

        foreach (var r in results)
        {
            Assert.NotNull(r.Id);
            Assert.NotEmpty(r.Id);
        }
    }

    // =========================================================================
    // Multi-query with weighted reranker
    // =========================================================================

    [Fact]
    public void MultiQuery_TwoDenseFields_WithWeightedReranker_ReturnsResults()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("mq_weighted"), schema =>
        {
            schema.AddVector("title_vec", 16, MetricType.Cosine);
            schema.AddVector("content_vec", 16, MetricType.Cosine);
        });

        var rng = new Random(42);
        for (int i = 0; i < 50; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("title_vec", RandomVector(16, rng));
            doc.SetVector("content_vec", RandomVector(16, rng));
            col.Insert(doc);
        }

        col.CreateIndex("title_vec");
        col.CreateIndex("content_vec");

        using var mq = new MultiQuery(topK: 5);
        mq.AddSubQuery("title_vec", RandomVector(16, new Random(99)))
          .AddSubQuery("content_vec", RandomVector(16, new Random(100)))
          .WithWeightedReranker(0.7, 0.3);

        var results = col.Query(mq);
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 5);
    }

    // =========================================================================
    // Multi-query with filter
    // =========================================================================

    [Fact]
    public void MultiQuery_WithFilter_ReturnsFilteredResults()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("mq_filter"), schema =>
        {
            schema.AddVector("v1", 16, MetricType.Cosine);
            schema.AddVector("v2", 16, MetricType.Cosine);
            schema.AddScalar("year", DataType.Int32);
        });

        var rng = new Random(42);
        for (int i = 0; i < 50; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("v1", RandomVector(16, rng));
            doc.SetVector("v2", RandomVector(16, rng));
            doc.SetInt32("year", 2000 + i);
            col.Insert(doc);
        }

        col.CreateIndex("v1");
        col.CreateIndex("v2");

        using var mq = new MultiQuery(topK: 10);
        mq.AddSubQuery("v1", RandomVector(16, new Random(99)))
          .AddSubQuery("v2", RandomVector(16, new Random(100)))
          .WithRrfReranker()
          .WithFilter("year >= 2040");

        var results = col.Query(mq);
        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            int docNum = int.Parse(r.Id.Split('_')[1]);
            Assert.True(2000 + docNum >= 2040, $"Expected year >= 2040 for {r.Id}");
        }
    }

    // =========================================================================
    // MultiQuery lifecycle
    // =========================================================================

    [Fact]
    public void MultiQuery_FluentChaining_ReturnsSameInstance()
    {
        using var mq = new MultiQuery(topK: 5);
        var r1 = mq.AddSubQuery("vec", new float[16]);
        Assert.Same(mq, r1);
        var r2 = mq.WithRrfReranker();
        Assert.Same(mq, r2);
    }

    [Fact]
    public void MultiQuery_Dispose_ThenAccess_ThrowsObjectDisposed()
    {
        var mq = new MultiQuery(topK: 5);
        mq.Dispose();
        Assert.Throws<ObjectDisposedException>(() => mq.AddSubQuery("vec", new float[16]));
    }

    [Fact]
    public void MultiQuery_DoubleDispose_Safe()
    {
        var mq = new MultiQuery(topK: 5);
        mq.AddSubQuery("vec", new float[16]);
        mq.WithRrfReranker();
        mq.Dispose();
        mq.Dispose();
    }

    // =========================================================================
    // Sparse vector document support
    // =========================================================================

    // Note: SetSparseVector on documents via add_field_by_value doesn't work --
    // sparse vectors need a dedicated C API function (not available in official c_api.h).
    // Sparse sub-queries via zvec_sub_query_set_sparse_vector DO work.

    // =========================================================================
    // Sparse vector schema
    // =========================================================================

    [Fact]
    public void AddSparseVector_ToSchema_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("sparse_test");
        try
        {
            var builder = new ZvecSchema(schemaHandle);
            builder.AddSparseVector("sparse_vec");
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    // =========================================================================
    // RRF rank constant variations
    // =========================================================================

    [Fact]
    public void RrfReranker_DifferentConstants_AllWork()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("rrf_k"), schema =>
        {
            schema.AddVector("v1", 8, MetricType.Cosine);
            schema.AddVector("v2", 8, MetricType.Cosine);
        });

        var rng = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("v1", RandomVector(8, rng));
            doc.SetVector("v2", RandomVector(8, rng));
            col.Insert(doc);
        }

        col.CreateIndex("v1");
        col.CreateIndex("v2");

        foreach (int k in new[] { 1, 10, 60, 100 })
        {
            using var mq = new MultiQuery(topK: 3);
            mq.AddSubQuery("v1", RandomVector(8, new Random(k)))
              .AddSubQuery("v2", RandomVector(8, new Random(k + 1)))
              .WithRrfReranker(rankConstant: k);

            var results = col.Query(mq);
            Assert.True(results.Count > 0, $"RRF k={k} returned no results");
        }
    }

    // =========================================================================
    // Multi-query with many sub-queries
    // =========================================================================

    [Fact]
    public void MultiQuery_ThreeFields_ReturnsResults()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("mq_three"), schema =>
        {
            schema.AddVector("v1", 8, MetricType.Cosine);
            schema.AddVector("v2", 8, MetricType.Cosine);
            schema.AddVector("v3", 8, MetricType.Cosine);
        });

        var rng = new Random(42);
        for (int i = 0; i < 30; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("v1", RandomVector(8, rng));
            doc.SetVector("v2", RandomVector(8, rng));
            doc.SetVector("v3", RandomVector(8, rng));
            col.Insert(doc);
        }

        col.CreateIndex("v1");
        col.CreateIndex("v2");
        col.CreateIndex("v3");

        using var mq = new MultiQuery(topK: 5);
        mq.AddSubQuery("v1", RandomVector(8, new Random(99)))
          .AddSubQuery("v2", RandomVector(8, new Random(100)))
          .AddSubQuery("v3", RandomVector(8, new Random(101)))
          .WithRrfReranker();

        var results = col.Query(mq);
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 5);
    }
}
