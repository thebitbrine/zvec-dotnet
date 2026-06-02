using Zvec;
using Zvec.Native;

namespace BasicSearch;

internal class Program
{
    static void Main()
    {
        string collectionPath = Path.Combine(Path.GetTempPath(), "zvec_sample_" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            Console.WriteLine("Creating collection...");

            using var collection = ZvecCollection.CreateAndOpen(collectionPath, schema =>
            {
                schema.AddVector("embedding", dimensions: 128, metric: MetricType.Cosine);
                schema.AddScalar("title", DataType.String);
                schema.AddScalar("year", DataType.Int32);
            });

            Console.WriteLine("Inserting documents...");

            string[] titles = { "The Matrix", "Inception", "Interstellar", "Blade Runner", "Arrival" };
            var random = new Random(42);

            for (int i = 0; i < titles.Length; i++)
            {
                using var doc = new ZvecDocument($"movie_{i}");

                // Random 128-dim vector (in practice, these would come from an embedding model)
                var embedding = new float[128];
                for (int j = 0; j < 128; j++)
                    embedding[j] = (float)(random.NextDouble() * 2 - 1);

                doc.SetVector("embedding", embedding);
                doc.SetString("title", titles[i]);
                doc.SetInt32("year", 1999 + i * 5);

                collection.Insert(doc);
            }

            Console.WriteLine($"Inserted {titles.Length} documents.");

            Console.WriteLine("Building index...");
            collection.CreateIndex("embedding");

            Console.WriteLine("Querying...");

            // Query with a random vector
            var queryVec = new float[128];
            for (int j = 0; j < 128; j++)
                queryVec[j] = (float)(random.NextDouble() * 2 - 1);

            using var query = VectorQuery.For("embedding", queryVec, topK: 3);
            var results = collection.Query(query);

            Console.WriteLine($"Top {results.Count} results:");
            foreach (var result in results)
            {
                Console.WriteLine($"  {result.Id} (score: {result.Score:F4})");
            }

            Console.WriteLine("Done.");
        }
        finally
        {
            // Cleanup temp collection
            try { Directory.Delete(collectionPath, true); } catch { }
        }
    }
}
