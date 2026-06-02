namespace Zvec.Tests;

[Collection("Zvec")]
public class SchemaEvolutionTests : IDisposable
{
    private readonly string _tempDir;

    public SchemaEvolutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_ddl_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name) => Path.Combine(_tempDir, name);

    // zvec schema evolution only supports numeric types (int32, int64, uint32, uint64, float, double)

    [Fact]
    public void AddColumn_Int32_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("add_i32"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
        });

        col.AddColumn("priority", DataType.Int32);
    }

    [Fact]
    public void AddColumn_Float_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("add_f"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
        });

        col.AddColumn("score", DataType.Float);
    }

    [Fact]
    public void AddColumn_Double_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("add_d"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
        });

        col.AddColumn("precise_score", DataType.Double);
    }

    [Fact]
    public void AddColumn_ThenInsertWithNewField_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("add_use"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
            schema.AddScalar("title", DataType.String);
        });

        using (var doc = new ZvecDocument("doc_1"))
        {
            doc.SetVector("vec", new float[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            doc.SetString("title", "original");
            col.Insert(doc);
        }

        col.AddColumn("priority", DataType.Int32, nullable: true);

        using (var doc = new ZvecDocument("doc_2"))
        {
            doc.SetVector("vec", new float[] { 8, 7, 6, 5, 4, 3, 2, 1 });
            doc.SetString("title", "with priority");
            doc.SetInt32("priority", 5);
            col.Insert(doc);
        }
    }

    [Fact]
    public void AddColumn_String_ThrowsNotSupported()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("add_str"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
        });

        // zvec only supports numeric types for add_column
        var ex = Assert.Throws<ZvecException>(() => col.AddColumn("label", DataType.String));
        Assert.NotEqual(ZvecErrorCode.Ok, ex.ErrorCode);
    }

    [Fact]
    public void DropColumn_NumericField_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("drop_col"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
            schema.AddScalar("count", DataType.Int32);
        });

        col.DropColumn("count");
    }

    [Fact]
    public void RenameColumn_NumericField_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("rename_col"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
            schema.AddScalar("old_count", DataType.Int32);
        });

        col.RenameColumn("old_count", "new_count");
    }
}
