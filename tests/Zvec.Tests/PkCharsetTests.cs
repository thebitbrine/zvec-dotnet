namespace Zvec.Tests;

/// <summary>
/// Documents which primary keys zvec accepts. v0.5.1 rejected some characters
/// (colon among them) and anything longer than 64 chars with "Invalid doc:
/// contains invalid characters"; v0.8.1 takes all of the probes below. Downstream
/// systems build deterministic PKs and need to know the real rules.
/// </summary>
[Collection("Zvec")]
public class PkCharsetTests : IDisposable
{
    private readonly string _tempDir;

    public PkCharsetTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_pk_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void ProbeCombinationPks()
    {
        using var col = ZvecCollection.CreateAndOpen(Path.Combine(_tempDir, "combo"), schema =>
        {
            schema.AddVector("vec", 4, MetricType.Cosine);
        });

        var probes = new (string Label, string Pk)[]
        {
            ("single-hash", "doc#1"),
            ("double-hash", "a#b#0"),
            ("hash-with-dot", "repo#file.cs#0"),
            ("hash-with-underscore-dash", "repo#zvec-dotnet_src_a#0"),
            ("failing-real-pk", "repo#zvec-dotnet_samples_BasicSearch_BasicSearch.csproj_7f5cd45d#0"),
            ("long-plain", new string('a', 80) + "0"),
            ("very-long-plain", new string('a', 200) + "0"),
            ("dot-heavy", "a.b.c.d.e.f#0"),
            ("leading-hash", "#doc1"),
            ("trailing-hash", "doc1#"),
            ("len-60", new string('b', 60)),
            ("len-63", new string('c', 63)),
            ("len-64", new string('d', 64)),
            ("len-65", new string('e', 65)),
            ("len-70", new string('f', 70)),
        };

        var ok = new List<string>();
        var rejected = new List<string>();
        foreach (var (label, pk) in probes)
        {
            using var doc = new ZvecDocument(pk);
            doc.SetVector("vec", new[] { 1f, 0f, 0f, 0f });
            try { col.Insert(doc); ok.Add(label); }
            catch (ZvecException) { rejected.Add(label); }
        }

        Console.WriteLine($"COMBO OK: {string.Join(", ", ok)}");
        Console.WriteLine($"COMBO REJECTED: {string.Join(", ", rejected)}");
        // knowledge pin: v0.8.1 accepts every probe, including pks past the old
        // 64-char limit. Update downstream pk builders if a zvec upgrade changes this.
        Assert.Empty(rejected);
        Assert.Equal(probes.Length, ok.Count);
    }

    [Fact]
    public void ProbeAllowedPkCharacters()
    {
        using var col = ZvecCollection.CreateAndOpen(Path.Combine(_tempDir, "probe"), schema =>
        {
            schema.AddVector("vec", 4, MetricType.Cosine);
        });

        var probes = new (string Label, string Pk)[]
        {
            ("plain", "doc1"),
            ("underscore", "doc_1"),
            ("hyphen", "doc-1"),
            ("colon", "doc:1"),
            ("dot", "doc.1"),
            ("slash", "doc/1"),
            ("backslash", "doc\\1"),
            ("hash", "doc#1"),
            ("pipe", "doc|1"),
            ("at", "doc@1"),
            ("space", "doc 1"),
            ("plus", "doc+1"),
            ("equals", "doc=1"),
            ("tilde", "doc~1"),
            ("unicode", "docé1"),
        };

        var ok = new List<string>();
        var rejected = new List<string>();

        foreach (var (label, pk) in probes)
        {
            using var doc = new ZvecDocument(pk);
            doc.SetVector("vec", new[] { 1f, 0f, 0f, 0f });
            try
            {
                col.Insert(doc);
                ok.Add(label);
            }
            catch (ZvecException)
            {
                rejected.Add(label);
            }
        }

        Console.WriteLine($"PK charset OK: {string.Join(", ", ok)}");
        Console.WriteLine($"PK charset REJECTED: {string.Join(", ", rejected)}");

        // knowledge pin: v0.8.1 accepts every probe (v0.5.1 rejected at least the
        // colon). Update downstream PK builders if this changes.
        Assert.Empty(rejected);
        Assert.Equal(probes.Length, ok.Count);

        // accepted is not enough - they have to round-trip through fetch
        using var fetched = col.Fetch(probes.Select(p => p.Pk).ToArray());
        Assert.Equal(probes.Select(p => p.Pk).OrderBy(x => x, StringComparer.Ordinal),
            fetched.Docs.Select(d => d.Pk).OrderBy(x => x, StringComparer.Ordinal));
    }
}
