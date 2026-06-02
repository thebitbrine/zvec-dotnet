namespace Zvec.Tests;

[Collection("Zvec")]
public class EdgeCaseTests : IDisposable
{
    private readonly string _tempDir;

    public EdgeCaseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_edge_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name) => Path.Combine(_tempDir, name);

    // =========================================================================
    // ObjectDisposedException coverage
    // =========================================================================

    [Fact]
    public void Collection_AfterDispose_InsertThrows()
    {
        var col = ZvecCollection.CreateAndOpen(CollPath("disp_ins"), schema =>
        {
            schema.AddVector("vec", 4);
        });
        col.Dispose();

        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("vec", new float[] { 1, 2, 3, 4 });

        Assert.Throws<ObjectDisposedException>(() => col.Insert(doc));
    }

    [Fact]
    public void Collection_AfterDispose_QueryThrows()
    {
        var col = ZvecCollection.CreateAndOpen(CollPath("disp_q"), schema =>
        {
            schema.AddVector("vec", 4);
        });
        col.Dispose();

        using var query = VectorQuery.For("vec", new float[] { 1, 2, 3, 4 }, 5);
        Assert.Throws<ObjectDisposedException>(() => col.Query(query));
    }

    [Fact]
    public void Collection_AfterDispose_FlushThrows()
    {
        var col = ZvecCollection.CreateAndOpen(CollPath("disp_fl"), schema =>
        {
            schema.AddVector("vec", 4);
        });
        col.Dispose();

        Assert.Throws<ObjectDisposedException>(() => col.Flush());
    }

    [Fact]
    public void Document_AfterDispose_SetVectorThrows()
    {
        var doc = new ZvecDocument("doc_1");
        doc.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            doc.SetVector("vec", new float[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void Document_AfterDispose_SetStringThrows()
    {
        var doc = new ZvecDocument("doc_1");
        doc.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            doc.SetString("title", "test"));
    }

    [Fact]
    public void Document_AfterDispose_SetInt32Throws()
    {
        var doc = new ZvecDocument("doc_1");
        doc.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            doc.SetInt32("count", 1));
    }

    [Fact]
    public void VectorQuery_AfterDispose_HandleThrows()
    {
        var query = VectorQuery.For("vec", new float[] { 1, 2, 3, 4 }, 5);
        query.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = query.Handle);
    }

    // =========================================================================
    // Double dispose safety
    // =========================================================================

    [Fact]
    public void Collection_DoubleDispose_Safe()
    {
        var col = ZvecCollection.CreateAndOpen(CollPath("dd_col"), schema =>
        {
            schema.AddVector("vec", 4);
        });
        col.Dispose();
        col.Dispose();
    }

    [Fact]
    public void Document_DoubleDispose_Safe()
    {
        var doc = new ZvecDocument("doc_1");
        doc.Dispose();
        doc.Dispose();
    }

    [Fact]
    public void VectorQuery_DoubleDispose_Safe()
    {
        var query = VectorQuery.For("vec", new float[] { 1, 2, 3, 4 }, 5);
        query.Dispose();
        query.Dispose();
    }

    // =========================================================================
    // High dimension vectors
    // =========================================================================

    [Fact]
    public void Insert_HighDimVector_2048_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("hi_dim"), schema =>
        {
            schema.AddVector("vec", 2048, MetricType.Cosine);
        });

        var vec = new float[2048];
        for (int i = 0; i < 2048; i++) vec[i] = i * 0.001f;

        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("vec", vec);
        col.Insert(doc);
    }

    // =========================================================================
    // Stress: create/close/reopen cycle
    // =========================================================================

    [Fact]
    public void CreateCloseReopen_100Cycles_NoLeaks()
    {
        string path = CollPath("cycle");

        using (var col = ZvecCollection.CreateAndOpen(path, schema =>
        {
            schema.AddVector("vec", 4, MetricType.Cosine);
        }))
        {
            col.Flush();
        }

        for (int i = 0; i < 100; i++)
        {
            using var col = ZvecCollection.Open(path);
            col.Flush();
        }
    }

    // =========================================================================
    // GC safety
    // =========================================================================

    [Fact]
    public void GCCollect_AfterDroppingReferences_NoCrash()
    {
        for (int i = 0; i < 5; i++)
        {
            var col = ZvecCollection.CreateAndOpen(CollPath($"gc_{i}"), schema =>
            {
                schema.AddVector("vec", 4);
            });
            col.Dispose();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
