namespace Zvec.Tests;

[Collection("Zvec")]
public class IndexTuningTests : IDisposable
{
    private readonly string _tempDir;

    public IndexTuningTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_idx_" + Guid.NewGuid().ToString("N")[..8]);
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

    private void InsertDocs(ZvecCollection col, int count, int dim)
    {
        var rng = new Random(42);
        for (int i = 0; i < count; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", RandomVector(dim, rng));
            col.Insert(doc);
        }
    }

    // =========================================================================
    // HNSW parameter tuning
    // =========================================================================

    [Fact]
    public void CreateHnswIndex_DefaultParams_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("hnsw_default"), schema =>
        {
            schema.AddVector("vec", 32, MetricType.Cosine);
        });

        InsertDocs(col, 100, 32);
        col.CreateHnswIndex("vec");

        using var query = VectorQuery.For("vec", RandomVector(32, new Random(99)), 5);
        var results = col.Query(query);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void CreateHnswIndex_CustomParams_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("hnsw_custom"), schema =>
        {
            schema.AddVector("vec", 32, MetricType.Cosine);
        });

        InsertDocs(col, 100, 32);
        col.CreateHnswIndex("vec", MetricType.Cosine, m: 32, efConstruction: 400);

        using var query = VectorQuery.For("vec", RandomVector(32, new Random(99)), 5);
        var results = col.Query(query);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void CreateHnswIndex_LowM_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("hnsw_low_m"), schema =>
        {
            schema.AddVector("vec", 16, MetricType.Cosine);
        });

        InsertDocs(col, 50, 16);
        col.CreateHnswIndex("vec", m: 4, efConstruction: 50);

        using var query = VectorQuery.For("vec", RandomVector(16, new Random(99)), 5);
        var results = col.Query(query);
        Assert.True(results.Count > 0);
    }

    // =========================================================================
    // Quantization
    // =========================================================================

    [Fact]
    public void CreateIndex_WithFp16Quantization_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("quant_fp16"), schema =>
        {
            schema.AddVector("vec", 32, MetricType.Cosine);
        });

        InsertDocs(col, 100, 32);
        col.CreateIndex("vec", IndexType.Hnsw, MetricType.Cosine, QuantizationType.Fp16);

        using var query = VectorQuery.For("vec", RandomVector(32, new Random(99)), 5);
        var results = col.Query(query);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void CreateIndex_WithInt8Quantization_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("quant_int8"), schema =>
        {
            schema.AddVector("vec", 32, MetricType.Cosine);
        });

        InsertDocs(col, 100, 32);
        col.CreateIndex("vec", IndexType.Hnsw, MetricType.Cosine, QuantizationType.Int8);

        using var query = VectorQuery.For("vec", RandomVector(32, new Random(99)), 5);
        var results = col.Query(query);
        Assert.Equal(5, results.Count);
    }

    // =========================================================================
    // QuantizationType enum values
    // =========================================================================

    [Theory]
    [InlineData(QuantizationType.Undefined, 0u)]
    [InlineData(QuantizationType.Fp16, 1u)]
    [InlineData(QuantizationType.Int8, 2u)]
    [InlineData(QuantizationType.Int4, 3u)]
    public void QuantizationType_HasCorrectValue(QuantizationType qt, uint expected)
    {
        Assert.Equal(expected, (uint)qt);
    }

    // =========================================================================
    // Drop index
    // =========================================================================

    [Fact]
    public void DropIndex_ExistingIndex_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("drop_idx"), schema =>
        {
            schema.AddVector("vec", 16, MetricType.Cosine);
        });

        InsertDocs(col, 20, 16);
        col.CreateIndex("vec");
        col.DropIndex("vec");
    }
}
