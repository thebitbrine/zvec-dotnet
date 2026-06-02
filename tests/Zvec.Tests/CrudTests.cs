namespace Zvec.Tests;

[Collection("Zvec")]
public class CrudTests : IDisposable
{
    private readonly string _tempDir;

    public CrudTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_crud_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name = "crud_col") => Path.Combine(_tempDir, name);

    private ZvecCollection CreateTestCollection(string name = "crud_col")
    {
        return ZvecCollection.CreateAndOpen(CollPath(name), schema =>
        {
            schema.AddVector("vec", 4, MetricType.Cosine);
            schema.AddScalar("title", DataType.String);
            schema.AddScalar("count", DataType.Int32);
        });
    }

    private static float[] MakeVector(float seed) => new[] { seed, seed + 0.1f, seed + 0.2f, seed + 0.3f };

    // =========================================================================
    // Document creation
    // =========================================================================

    [Fact]
    public void CreateDocument_WithPK_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        Assert.NotNull(doc);
    }

    [Fact]
    public void SetVector_FP32_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("vec", new float[] { 1.0f, 2.0f, 3.0f, 4.0f });
    }

    [Fact]
    public void SetString_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetString("title", "hello world");
    }

    [Fact]
    public void SetInt32_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetInt32("count", 42);
    }

    [Fact]
    public void SetInt64_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetInt64("big", 9_999_999_999L);
    }

    [Fact]
    public void SetFloat_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetFloat("score", 3.14f);
    }

    [Fact]
    public void SetDouble_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetDouble("precise", 3.14159265358979);
    }

    [Fact]
    public void SetBool_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetBool("active", true);
    }

    [Fact]
    public void DocumentDispose_ThenAccess_ThrowsObjectDisposed()
    {
        var doc = new ZvecDocument("doc_1");
        doc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => doc.SetString("title", "test"));
    }

    // =========================================================================
    // Insert
    // =========================================================================

    [Fact]
    public void Insert_SingleDocument_Succeeds()
    {
        using var col = CreateTestCollection("ins_single");
        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("vec", MakeVector(1.0f));
        doc.SetString("title", "first doc");
        doc.SetInt32("count", 1);

        col.Insert(doc);
    }

    [Fact]
    public void Insert_MultipleDocuments_Succeeds()
    {
        using var col = CreateTestCollection("ins_multi");

        var docs = new List<ZvecDocument>();
        try
        {
            for (int i = 0; i < 10; i++)
            {
                var doc = new ZvecDocument($"doc_{i}");
                doc.SetVector("vec", MakeVector(i * 0.1f));
                doc.SetString("title", $"Document {i}");
                doc.SetInt32("count", i);
                docs.Add(doc);
            }

            var (success, errors) = col.Insert(docs);
            Assert.Equal(10u, (uint)success);
            Assert.Equal(0u, (uint)errors);
        }
        finally
        {
            foreach (var d in docs) d.Dispose();
        }
    }

    [Fact]
    public void Insert_100Documents_Succeeds()
    {
        using var col = CreateTestCollection("ins_100");

        var docs = new List<ZvecDocument>();
        try
        {
            for (int i = 0; i < 100; i++)
            {
                var doc = new ZvecDocument($"doc_{i}");
                doc.SetVector("vec", MakeVector(i * 0.01f));
                doc.SetString("title", $"Document {i}");
                doc.SetInt32("count", i);
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

    [Fact]
    public void Insert_DocStillValidAfter_CanDispose()
    {
        using var col = CreateTestCollection("ins_borrow");
        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("vec", MakeVector(1.0f));
        doc.SetString("title", "before insert");
        doc.SetInt32("count", 1);

        col.Insert(doc);

        // Doc should still be valid -- insert borrows, doesn't take ownership
        // We verify this by checking the handle accessor doesn't throw
        Assert.NotEqual(0, doc.Handle);
    }

    // =========================================================================
    // Upsert
    // =========================================================================

    [Fact]
    public void Upsert_NewDocument_Succeeds()
    {
        using var col = CreateTestCollection("ups_new");
        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("vec", MakeVector(1.0f));
        doc.SetString("title", "upserted");
        doc.SetInt32("count", 1);

        col.Upsert(doc);
    }

    [Fact]
    public void Upsert_ExistingDocument_Succeeds()
    {
        using var col = CreateTestCollection("ups_exist");

        using (var doc1 = new ZvecDocument("doc_1"))
        {
            doc1.SetVector("vec", MakeVector(1.0f));
            doc1.SetString("title", "original");
            doc1.SetInt32("count", 1);
            col.Insert(doc1);
        }

        using (var doc2 = new ZvecDocument("doc_1"))
        {
            doc2.SetVector("vec", MakeVector(2.0f));
            doc2.SetString("title", "updated");
            doc2.SetInt32("count", 2);
            col.Upsert(doc2);
        }
    }

    // =========================================================================
    // Update
    // =========================================================================

    [Fact]
    public void Update_ExistingDocument_Succeeds()
    {
        using var col = CreateTestCollection("upd_exist");

        using (var doc = new ZvecDocument("doc_1"))
        {
            doc.SetVector("vec", MakeVector(1.0f));
            doc.SetString("title", "original");
            doc.SetInt32("count", 1);
            col.Insert(doc);
        }

        using (var doc = new ZvecDocument("doc_1"))
        {
            doc.SetVector("vec", MakeVector(2.0f));
            doc.SetString("title", "updated");
            doc.SetInt32("count", 2);
            col.Update(doc);
        }
    }

    // =========================================================================
    // Delete
    // =========================================================================

    [Fact]
    public void Delete_ExistingPK_Succeeds()
    {
        using var col = CreateTestCollection("del_exist");

        using (var doc = new ZvecDocument("doc_1"))
        {
            doc.SetVector("vec", MakeVector(1.0f));
            doc.SetString("title", "to delete");
            doc.SetInt32("count", 1);
            col.Insert(doc);
        }

        col.Delete("doc_1");
    }

    [Fact]
    public void Delete_NonExistentPK_DoesNotThrow()
    {
        using var col = CreateTestCollection("del_missing");

        // Deleting a non-existent PK should not throw
        col.Delete("does_not_exist");
    }
}
