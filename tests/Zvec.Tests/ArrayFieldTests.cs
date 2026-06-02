namespace Zvec.Tests;

[Collection("Zvec")]
public class ArrayFieldTests : IDisposable
{
    private readonly string _tempDir;

    public ArrayFieldTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_array_" + Guid.NewGuid().ToString("N")[..8]);
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
    // Array field on documents
    // =========================================================================

    [Fact]
    public void SetStringArray_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetStringArray("tags", new[] { "science", "physics", "quantum" });
    }

    [Fact]
    public void SetStringArray_Empty_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetStringArray("tags", Array.Empty<string>());
    }

    [Fact]
    public void SetInt32Array_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetInt32Array("ids", new[] { 1, 2, 3, 42 });
    }

    [Fact]
    public void SetInt64Array_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetInt64Array("big_ids", new[] { 100L, 200L, 999_999_999L });
    }

    [Fact]
    public void SetFloatArray_Succeeds()
    {
        using var doc = new ZvecDocument("doc_1");
        doc.SetFloatArray("scores", new[] { 0.1f, 0.5f, 0.9f });
    }

    // =========================================================================
    // Array field in schema + insert
    // =========================================================================

    [Fact]
    public void ArrayStringField_Schema_Insert_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("arr_str"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
            schema.AddArray("tags", DataType.ArrayString);
        });

        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("vec", RandomVector(8, new Random(42)));
        doc.SetStringArray("tags", new[] { "machine-learning", "nlp", "transformers" });
        col.Insert(doc);
    }

    [Fact]
    public void ArrayStringField_Indexed_Insert_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("arr_str_idx"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
            schema.AddArray("concepts", DataType.ArrayString, indexed: true);
        });

        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("vec", RandomVector(8, new Random(42)));
        doc.SetStringArray("concepts", new[] { "physics", "quantum", "entanglement" });
        col.Insert(doc);
    }

    [Fact]
    public void ArrayInt32Field_Insert_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("arr_int"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
            schema.AddArray("category_ids", DataType.ArrayInt32);
        });

        using var doc = new ZvecDocument("doc_1");
        doc.SetVector("vec", RandomVector(8, new Random(42)));
        doc.SetInt32Array("category_ids", new[] { 1, 5, 42 });
        col.Insert(doc);
    }

    // CONTAIN_ANY filter syntax: "fieldname CONTAIN_ANY ('value1', 'value2')"
    // Needs further investigation on invert index + string array interop.
    // The filter parses correctly but returns 0 results -- likely the string array
    // data format via zvec_string_array_t needs different handling for invert indexing.

    // =========================================================================
    // Multiple inserts with array fields
    // =========================================================================

    [Fact]
    public void ArrayField_BatchInsert_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath("arr_batch"), schema =>
        {
            schema.AddVector("vec", 8, MetricType.Cosine);
            schema.AddArray("tags", DataType.ArrayString, indexed: true);
            schema.AddArray("scores", DataType.ArrayFloat);
        });

        var rng = new Random(42);
        var docs = new List<ZvecDocument>();
        try
        {
            for (int i = 0; i < 50; i++)
            {
                var doc = new ZvecDocument($"doc_{i}");
                doc.SetVector("vec", RandomVector(8, rng));
                doc.SetStringArray("tags", new[] { $"tag_{i % 5}", $"category_{i % 3}" });
                doc.SetFloatArray("scores", new[] { (float)rng.NextDouble(), (float)rng.NextDouble() });
                docs.Add(doc);
            }

            var (success, errors) = col.Insert(docs);
            Assert.Equal(50u, (uint)success);
            Assert.Equal(0u, (uint)errors);
        }
        finally
        {
            foreach (var d in docs) d.Dispose();
        }
    }
}
