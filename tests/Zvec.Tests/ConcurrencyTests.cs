namespace Zvec.Tests;

[Collection("Zvec")]
public class ConcurrencyTests : IDisposable
{
    private readonly string _tempDir;

    public ConcurrencyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_conc_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name) => Path.Combine(_tempDir, name);

    private static float[] NormalizeVector(float[] v)
    {
        float norm = 0;
        for (int i = 0; i < v.Length; i++) norm += v[i] * v[i];
        norm = MathF.Sqrt(norm);
        var result = new float[v.Length];
        for (int i = 0; i < v.Length; i++) result[i] = v[i] / norm;
        return result;
    }

    [Fact]
    public void ConcurrentReads_10Threads_NoCrash()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("conc_read"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
        });

        // Insert docs
        for (int i = 0; i < 50; i++)
        {
            var vec = new float[8];
            for (int j = 0; j < 8; j++) vec[j] = i + j * 0.1f;
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", NormalizeVector(vec));
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        // Concurrent queries
        var threads = new Thread[10];
        var errors = new Exception?[10];

        for (int t = 0; t < 10; t++)
        {
            int threadIdx = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    var qvec = new float[8];
                    for (int j = 0; j < 8; j++) qvec[j] = threadIdx + j * 0.1f;
                    using var query = VectorQuery.For("vec", NormalizeVector(qvec), 5);
                    var results = col.Query(query);
                    Assert.True(results.Count > 0);
                    Assert.True(results.Count <= 5);
                }
                catch (Exception ex) { errors[threadIdx] = ex; }
            });
            threads[t].Start();
        }

        foreach (var thread in threads) thread.Join();
        foreach (var err in errors) Assert.Null(err);
    }

    [Fact]
    public void LargeBatchInsert_10000Docs_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("batch_10k"), schema =>
        {
            schema.AddVector("vec", 16, MetricType.Cosine);
        });

        // Insert in batches of 100
        for (int batch = 0; batch < 100; batch++)
        {
            var docs = new List<ZvecDocument>();
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    int idx = batch * 100 + i;
                    var vec = new float[16];
                    for (int j = 0; j < 16; j++) vec[j] = idx * 0.01f + j * 0.001f;
                    var doc = new ZvecDocument($"doc_{idx}");
                    doc.SetVector("vec", vec);
                    docs.Add(doc);
                }

                var (success, errors) = col.Insert(docs);
                Assert.Equal(100u, (uint)success);
                Assert.Equal(0u, (uint)errors);
            }
            finally
            {
                foreach (var d in docs) d.Dispose();
            }
        }

        col.Flush();
    }
}
