namespace Zvec.Tests;

[Collection("Zvec")]
public class FetchTests : IDisposable
{
    private readonly string _tempDir;

    public FetchTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_fetch_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private ZvecCollection CreateCollection(string name)
    {
        var col = ZvecCollection.CreateAndOpen(Path.Combine(_tempDir, name), schema =>
        {
            schema.AddVector("vec", 4, MetricType.Cosine);
            schema.AddScalar("title", DataType.String);
            schema.AddScalar("updated_at", DataType.Int64);
        });

        for (int i = 0; i < 5; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", new[] { 1f, 0f, 0f, i * 0.1f });
            doc.SetString("title", $"title number {i}");
            doc.SetInt64("updated_at", 1700000000 + i);
            col.Insert(doc);
        }

        return col;
    }

    [Fact]
    public void Fetch_ByPks_ReturnsFieldValues()
    {
        using var col = CreateCollection("f_basic");

        using var result = col.Fetch(new[] { "doc_1", "doc_3" });

        Assert.Equal(2, result.Docs.Count);
        var byPk = result.Docs.ToDictionary(d => d.Pk);
        Assert.Equal("title number 1", byPk["doc_1"].GetString("title"));
        Assert.Equal("title number 3", byPk["doc_3"].GetString("title"));
        Assert.Equal(1700000001, byPk["doc_1"].GetInt64("updated_at"));
    }

    [Fact]
    public void Fetch_MissingKey_SilentlyAbsent()
    {
        using var col = CreateCollection("f_missing");

        using var result = col.Fetch(new[] { "doc_0", "nope_404" });

        Assert.Single(result.Docs);
        Assert.Equal("doc_0", result.Docs[0].Pk);
    }

    [Fact]
    public void Fetch_OutputFieldsSubset_OtherFieldsAbsent()
    {
        using var col = CreateCollection("f_subset");

        using var result = col.Fetch(new[] { "doc_2" }, outputFields: new[] { "title" });

        Assert.Single(result.Docs);
        Assert.Equal("title number 2", result.Docs[0].GetString("title"));
        // updated_at not requested - must read as absent, not garbage
        Assert.Null(result.Docs[0].GetInt64("updated_at"));
    }

    [Fact]
    public void Fetch_IncludeVector_RoundTripsVector()
    {
        using var col = CreateCollection("f_vec");

        using var result = col.Fetch(new[] { "doc_2" }, includeVector: true);

        Assert.Single(result.Docs);
        var vec = result.Docs[0].GetVector("vec");
        Assert.NotNull(vec);
        Assert.Equal(4, vec!.Length);
        Assert.Equal(0.2f, vec[3], precision: 4); // doc_2 seeded with i*0.1 in slot 3
    }

    [Fact]
    public void EmptyStringField_RoundTrips()
    {
        // regression: fixed() on an empty byte[] pins a null pointer; SetString("")
        // used to throw "Invalid arguments: null pointer"
        using var col = CreateCollection("f_empty");

        using (var doc = new ZvecDocument("empty_1"))
        {
            doc.SetVector("vec", new[] { 1f, 0f, 0f, 0f });
            doc.SetString("title", "");
            doc.SetInt64("updated_at", 1);
            col.Insert(doc);
        }

        using var result = col.Fetch(new[] { "empty_1" });
        Assert.Single(result.Docs);
        Assert.Equal("", result.Docs[0].GetString("title"));
    }

    [Fact]
    public void Fetch_AfterDispose_Throws()
    {
        using var col = CreateCollection("f_disposed");

        var result = col.Fetch(new[] { "doc_0" });
        var doc = result.Docs[0];
        result.Dispose();

        Assert.Throws<ObjectDisposedException>(() => doc.GetString("title"));
    }
}
