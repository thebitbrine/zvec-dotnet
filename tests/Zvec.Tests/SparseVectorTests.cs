namespace Zvec.Tests;

[Collection("Zvec")]
public class SparseVectorTests : IDisposable
{
    private readonly string _tempDir;

    public SparseVectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_sparse_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name) => Path.Combine(_tempDir, name);

    [Fact]
    public void SetSparseVector_OnDocument_Succeeds()
    {
        using var doc = new ZvecDocument("sparse_doc");
        doc.SetSparseVector("sparse_field", new Dictionary<uint, float>
        {
            [42] = 0.8f,
            [99] = 0.3f,
            [1024] = 1.5f,
        });
    }

    [Fact]
    public void SetSparseVector_EmptyDict_Succeeds()
    {
        using var doc = new ZvecDocument("empty_sparse");
        doc.SetSparseVector("sparse_field", new Dictionary<uint, float>());
    }

    [Fact]
    public void SetSparseVector_SingleEntry_Succeeds()
    {
        using var doc = new ZvecDocument("single_sparse");
        doc.SetSparseVector("sparse_field", new Dictionary<uint, float>
        {
            [0] = 1.0f,
        });
    }

    [Fact]
    public void SetSparseVector_LargeIndices_Succeeds()
    {
        using var doc = new ZvecDocument("large_idx");
        doc.SetSparseVector("sparse_field", new Dictionary<uint, float>
        {
            [0] = 0.1f,
            [50000] = 0.5f,
            [100000] = 0.9f,
        });
    }

    [Fact]
    public void SparseVector_InsertIntoCollection_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("sparse_col"), schema =>
        {
            schema.AddVector("dense", 8, MetricType.Cosine);
            schema.AddSparseVector("sparse");
        });

        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("dense", new float[] { 1, 0, 0, 0, 0, 0, 0, 0 });
        doc.SetSparseVector("sparse", new Dictionary<uint, float>
        {
            [10] = 0.8f,
            [20] = 0.3f,
        });
        col.Insert(doc);
    }

    [Fact]
    public void SparseVector_InsertMultiple_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("sparse_multi"), schema =>
        {
            schema.AddVector("dense", 8, MetricType.Cosine);
            schema.AddSparseVector("sparse");
        });

        var rng = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            var dense = new float[8];
            for (int j = 0; j < 8; j++) dense[j] = (float)(rng.NextDouble() * 2 - 1);
            doc.SetVector("dense", dense);

            var sparse = new Dictionary<uint, float>();
            for (int j = 0; j < 5; j++)
                sparse[(uint)rng.Next(0, 10000)] = (float)rng.NextDouble();
            doc.SetSparseVector("sparse", sparse);

            col.Insert(doc);
        }
    }
}
