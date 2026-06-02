namespace Zvec.Tests;

[Collection("Zvec")]
public class FilterTests : IDisposable
{
    private readonly string _tempDir;

    public FilterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_filter_" + Guid.NewGuid().ToString("N")[..8]);
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

    private ZvecCollection CreateFilterCollection(string name)
    {
        var col = ZvecCollection.CreateAndOpen(CollPath(name), schema =>
        {
            schema.AddVector("vec", 16, MetricType.Cosine);
            schema.AddScalar("category", DataType.String);
            schema.AddScalar("year", DataType.Int32);
        });

        var rng = new Random(42);
        for (int i = 0; i < 50; i++)
        {
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", RandomVector(16, rng));
            doc.SetString("category", i % 2 == 0 ? "science" : "history");
            doc.SetInt32("year", 2000 + i);
            col.Insert(doc);
        }

        col.CreateIndex("vec");
        return col;
    }

    [Fact]
    public void WithFilter_StringEquals_ReturnsFilteredResults()
    {
        using var col = CreateFilterCollection("f_str");

        var rng = new Random(99);
        using var query = VectorQuery.For("vec", RandomVector(16, rng), 10)
            .WithFilter("category = 'science'");

        var results = col.Query(query);

        Assert.NotEmpty(results);
        // All results should be even-numbered docs (science category)
        foreach (var r in results)
        {
            int docNum = int.Parse(r.Id.Split('_')[1]);
            Assert.True(docNum % 2 == 0, $"Expected science doc (even), got {r.Id}");
        }
    }

    [Fact]
    public void WithFilter_IntComparison_ReturnsFilteredResults()
    {
        using var col = CreateFilterCollection("f_int");

        var rng = new Random(99);
        using var query = VectorQuery.For("vec", RandomVector(16, rng), 10)
            .WithFilter("year >= 2040");

        var results = col.Query(query);

        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            int docNum = int.Parse(r.Id.Split('_')[1]);
            int year = 2000 + docNum;
            Assert.True(year >= 2040, $"Expected year >= 2040, got {year} for {r.Id}");
        }
    }

    [Fact]
    public void WithFilter_CompoundExpression_Works()
    {
        using var col = CreateFilterCollection("f_compound");

        var rng = new Random(99);
        using var query = VectorQuery.For("vec", RandomVector(16, rng), 10)
            .WithFilter("category = 'science' AND year >= 2020");

        var results = col.Query(query);

        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            int docNum = int.Parse(r.Id.Split('_')[1]);
            Assert.True(docNum % 2 == 0, $"Expected science doc, got {r.Id}");
            Assert.True(2000 + docNum >= 2020, $"Expected year >= 2020 for {r.Id}");
        }
    }

    [Fact]
    public void WithFilter_NoMatches_ReturnsEmpty()
    {
        using var col = CreateFilterCollection("f_none");

        var rng = new Random(99);
        using var query = VectorQuery.For("vec", RandomVector(16, rng), 10)
            .WithFilter("year > 9999");

        var results = col.Query(query);

        Assert.Empty(results);
    }

    [Fact]
    public void WithFilter_FluentChaining_Works()
    {
        using var col = CreateFilterCollection("f_chain");

        var rng = new Random(99);
        var vec = RandomVector(16, rng);

        // Verify fluent API returns same object
        using var query = VectorQuery.For("vec", vec, 5);
        var returned = query.WithFilter("year > 2010");
        Assert.Same(query, returned);
    }
}
