using Zvec;
using Zvec.Native;

namespace BasicSearch;

internal class Program
{
    private const int Dim = 128;

    // (id, title, team). In practice the embedding comes from a model; here every
    // team gets its own direction in vector space so the results are predictable.
    private static readonly (string Id, string Title, string Team)[] Notes =
    {
        ("n1", "Restore hangs after manifest load, set LEGACY_FETCHER=1", "infra"),
        ("n2", "Checkpoint stalls on the NFS mount when prefetch is too high", "infra"),
        ("n3", "Restart the supervisor after the nightly backup completes", "infra"),
        ("n4", "Checkout page shows the wrong currency for Armenian cards", "web"),
        ("n5", "Cart badge does not update after removing the last item", "web"),
        ("n6", "Quarterly revenue projections for the hosting business", "finance"),
    };

    static int Main()
    {
        string collectionPath = Path.Combine(Path.GetTempPath(), "zvec_sample_" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            Console.WriteLine($"zvec {ZvecRuntime.Version}");

            using var collection = ZvecCollection.CreateAndOpen(collectionPath, schema =>
            {
                schema.AddVector("embedding", dimensions: Dim, metric: MetricType.Cosine);
                schema.AddText("title");                      // full-text indexed
                schema.AddScalar("team", DataType.String);
            });

            foreach (var (id, title, team) in Notes)
            {
                using var doc = new ZvecDocument(id);
                doc.SetVector("embedding", Embed(team, id));
                doc.SetString("title", title);
                doc.SetString("team", team);
                collection.Insert(doc);
            }

            Console.WriteLine($"Inserted {collection.GetStats().DocCount} documents.");

            // 1. Vector search
            using (var query = VectorQuery.For("embedding", Embed("infra", "query"), topK: 3))
                Print("Vector search (infra-like):", collection.Query(query));

            // 2. Keyword search
            using (var query = VectorQuery.ForFts("title", topK: 3).WithFtsMatch("checkout currency"))
                Print("Keyword search (checkout currency):", collection.Query(query));

            // 3. Hybrid: both in one query, fused with reciprocal rank fusion
            using (var query = new MultiQuery(topK: 4)
                .AddSubQuery("embedding", Embed("infra", "query"), numCandidates: 3)
                .AddFtsSubQuery("title", "checkout currency", numCandidates: 3)
                .WithRrfReranker())
            {
                var hybrid = collection.Query(query);
                Print("Hybrid (vector + keyword):", hybrid);

                // the keyword-only hit has to make it in next to the vector hits
                if (!hybrid.Any(r => r.Id == "n4") || !hybrid.Any(r => r.Id is "n1" or "n2" or "n3"))
                {
                    Console.WriteLine("FAILED: hybrid result is missing one side");
                    return 1;
                }
            }

            // 4. Best match per team
            using (var query = GroupByQuery.For("embedding", Embed("web", "query"), "team",
                groupCount: 3, topKPerGroup: 1))
            {
                Console.WriteLine("Best match per team:");
                foreach (var group in collection.Query(query))
                    Console.WriteLine($"  {group.Value}: {group.Results[0].Id}");
            }

            Console.WriteLine("Done.");
            return 0;
        }
        finally
        {
            try { Directory.Delete(collectionPath, true); } catch { }
        }
    }

    // Deterministic stand-in for an embedding model: a strong component per team
    // plus a little per-text noise.
    private static float[] Embed(string team, string text)
    {
        var v = new float[Dim];
        var noise = new Random(StableSeed(text));
        for (int i = 0; i < Dim; i++)
            v[i] = (float)(noise.NextDouble() * 0.2 - 0.1);

        var direction = new Random(StableSeed(team));
        for (int i = 0; i < Dim; i++)
            v[i] += (float)(direction.NextDouble() * 2 - 1);

        return v;
    }

    // string.GetHashCode is randomized per process, so roll a stable one
    private static int StableSeed(string s)
    {
        int h = 17;
        foreach (char c in s) h = unchecked(h * 31 + c);
        return h;
    }

    private static void Print(string title, IReadOnlyList<SearchResult> results)
    {
        Console.WriteLine(title);
        foreach (var r in results)
            Console.WriteLine($"  {r.Id} (score: {r.Score:F4})");
    }
}
