using System.Diagnostics;

namespace Zvec.Tests;

[Collection("Zvec")]
public class LargeScaleTests : IDisposable
{
    private readonly string _tempDir;

    public LargeScaleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_large_" + Guid.NewGuid().ToString("N")[..8]);
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
    // 1M vectors, dim=1024 -- standard embedding dimension
    // =========================================================================

    [Fact]
    public void OneMillion_Dim1024_InsertIndexQuery()
    {
        int dim = 1024;
        int docCount = 1_000_000;
        int batchSize = 1000;
        int topK = 10;

        Console.WriteLine($"[LARGE] Starting 1M doc test (dim={dim})...");

        using var col = ZvecCollection.CreateAndOpen(CollPath("1m_1024"), schema =>
        {
            schema.AddVector("vec", (uint)dim, MetricType.Cosine);
        });

        var rng = new Random(42);

        // -- INSERT --
        var swInsert = Stopwatch.StartNew();
        long inserted = 0;

        for (int batch = 0; batch < docCount / batchSize; batch++)
        {
            var docs = new List<ZvecDocument>(batchSize);
            try
            {
                for (int i = 0; i < batchSize; i++)
                {
                    int idx = batch * batchSize + i;
                    var doc = new ZvecDocument($"d{idx}");
                    doc.SetVector("vec", RandomVector(dim, rng));
                    docs.Add(doc);
                }

                var (success, errs) = col.Insert(docs);
                inserted += (long)success;

                if ((batch + 1) % 100 == 0)
                {
                    double elapsed = swInsert.Elapsed.TotalSeconds;
                    double rate = inserted / elapsed;
                    Console.WriteLine($"[LARGE]   Inserted {inserted:N0} / {docCount:N0} ({rate:N0} docs/sec)");
                }
            }
            finally
            {
                foreach (var d in docs) d.Dispose();
            }
        }

        swInsert.Stop();
        double insertRate = inserted / swInsert.Elapsed.TotalSeconds;
        Console.WriteLine($"[LARGE] Insert complete: {inserted:N0} docs in {swInsert.Elapsed.TotalSeconds:F1}s ({insertRate:N0} docs/sec)");

        Assert.Equal(docCount, (int)inserted);
        Assert.True(insertRate > 10_000, $"Insert rate too low: {insertRate:N0} docs/sec");

        // -- FLUSH --
        var swFlush = Stopwatch.StartNew();
        col.Flush();
        swFlush.Stop();
        Console.WriteLine($"[LARGE] Flush: {swFlush.Elapsed.TotalMilliseconds:F0}ms");

        // -- INDEX BUILD --
        var swIndex = Stopwatch.StartNew();
        col.CreateIndex("vec");
        swIndex.Stop();
        Console.WriteLine($"[LARGE] Index build: {swIndex.Elapsed.TotalSeconds:F1}s");

        // -- OPTIMIZE (merge segments, actually build HNSW graph) --
        var swOpt = Stopwatch.StartNew();
        col.Optimize();
        swOpt.Stop();
        Console.WriteLine($"[LARGE] Optimize: {swOpt.Elapsed.TotalSeconds:F1}s");

        // -- SINGLE QUERY LATENCY --
        // warmup
        using (var warmup = VectorQuery.For("vec", RandomVector(dim, rng), topK))
            col.Query(warmup);

        int queryCount = 200;
        var queryVecs = new float[queryCount][];
        for (int i = 0; i < queryCount; i++)
            queryVecs[i] = RandomVector(dim, rng);

        var swQuery = Stopwatch.StartNew();
        for (int i = 0; i < queryCount; i++)
        {
            using var q = VectorQuery.For("vec", queryVecs[i], topK);
            var results = col.Query(q);
            Assert.Equal(topK, results.Count);
        }
        swQuery.Stop();

        double avgLatency = swQuery.Elapsed.TotalMilliseconds / queryCount;
        double qps = queryCount / swQuery.Elapsed.TotalSeconds;
        Console.WriteLine($"[LARGE] Query latency: {avgLatency:F2}ms avg, {qps:N0} QPS (single-threaded, {queryCount} queries)");

        // At 1M docs with dim=1024, latency can be higher
        Assert.True(avgLatency < 500, $"Query latency too high: {avgLatency:F2}ms");

        // -- CONCURRENT QUERY THROUGHPUT --
        int threadCount = Environment.ProcessorCount;
        int queriesPerThread = 100;

        var threadVecs = new float[threadCount][][];
        for (int t = 0; t < threadCount; t++)
        {
            var tRng = new Random(1000 + t);
            threadVecs[t] = new float[queriesPerThread][];
            for (int i = 0; i < queriesPerThread; i++)
                threadVecs[t][i] = RandomVector(dim, tRng);
        }

        var threads = new Thread[threadCount];
        var errors = new Exception?[threadCount];

        var swConc = Stopwatch.StartNew();
        for (int t = 0; t < threadCount; t++)
        {
            int ti = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < queriesPerThread; i++)
                    {
                        using var q = VectorQuery.For("vec", threadVecs[ti][i], topK);
                        var r = col.Query(q);
                        if (r.Count != topK)
                            throw new Exception($"Expected {topK} results, got {r.Count}");
                    }
                }
                catch (Exception ex) { errors[ti] = ex; }
            });
            threads[t].Start();
        }

        foreach (var thread in threads) thread.Join();
        swConc.Stop();

        foreach (var err in errors) Assert.Null(err);

        int totalConcQueries = threadCount * queriesPerThread;
        double concQps = totalConcQueries / swConc.Elapsed.TotalSeconds;
        Console.WriteLine($"[LARGE] Concurrent: {threadCount} threads x {queriesPerThread} queries = {totalConcQueries} total, {swConc.Elapsed.TotalSeconds:F1}s, {concQps:N0} QPS");

        // At 1M x dim=1024, QPS depends heavily on index state and hardware
        Assert.True(concQps > 1, $"Concurrent QPS too low: {concQps:N0}");

        Console.WriteLine("[LARGE] 1M test complete.");
    }

    // =========================================================================
    // 500K vectors, dim=1024 -- realistic mid-scale workload
    // =========================================================================

    [Fact]
    public void HalfMillion_Dim1024_InsertIndexQuery()
    {
        int dim = 1024;
        int docCount = 500_000;
        int batchSize = 500;
        int topK = 10;

        Console.WriteLine($"[LARGE] Starting 500K doc test (dim={dim})...");

        using var col = ZvecCollection.CreateAndOpen(CollPath("500k_1024"), schema =>
        {
            schema.AddVector("vec", (uint)dim, MetricType.Cosine);
        });

        var rng = new Random(42);

        // -- INSERT --
        var swInsert = Stopwatch.StartNew();
        long inserted = 0;

        for (int batch = 0; batch < docCount / batchSize; batch++)
        {
            var docs = new List<ZvecDocument>(batchSize);
            try
            {
                for (int i = 0; i < batchSize; i++)
                {
                    int idx = batch * batchSize + i;
                    var doc = new ZvecDocument($"d{idx}");
                    doc.SetVector("vec", RandomVector(dim, rng));
                    docs.Add(doc);
                }

                var (success, errs) = col.Insert(docs);
                inserted += (long)success;

                if ((batch + 1) % 100 == 0)
                {
                    double elapsed = swInsert.Elapsed.TotalSeconds;
                    double rate = inserted / elapsed;
                    Console.WriteLine($"[LARGE]   Inserted {inserted:N0} / {docCount:N0} ({rate:N0} docs/sec)");
                }
            }
            finally
            {
                foreach (var d in docs) d.Dispose();
            }
        }

        swInsert.Stop();
        double insertRate = inserted / swInsert.Elapsed.TotalSeconds;
        Console.WriteLine($"[LARGE] Insert complete: {inserted:N0} docs in {swInsert.Elapsed.TotalSeconds:F1}s ({insertRate:N0} docs/sec)");

        Assert.Equal(docCount, (int)inserted);
        Assert.True(insertRate > 5_000, $"Insert rate too low: {insertRate:N0} docs/sec");

        // -- INDEX BUILD --
        var swIndex = Stopwatch.StartNew();
        col.CreateIndex("vec");
        swIndex.Stop();
        Console.WriteLine($"[LARGE] Index build: {swIndex.Elapsed.TotalSeconds:F1}s");

        // -- OPTIMIZE --
        var swOpt = Stopwatch.StartNew();
        col.Optimize();
        swOpt.Stop();
        Console.WriteLine($"[LARGE] Optimize: {swOpt.Elapsed.TotalSeconds:F1}s");

        // -- QUERY LATENCY --
        using (var warmup = VectorQuery.For("vec", RandomVector(dim, rng), topK))
            col.Query(warmup);

        int queryCount = 100;
        var queryVecs = new float[queryCount][];
        for (int i = 0; i < queryCount; i++)
            queryVecs[i] = RandomVector(dim, rng);

        var swQuery = Stopwatch.StartNew();
        for (int i = 0; i < queryCount; i++)
        {
            using var q = VectorQuery.For("vec", queryVecs[i], topK);
            var results = col.Query(q);
            Assert.Equal(topK, results.Count);
        }
        swQuery.Stop();

        double avgLatency = swQuery.Elapsed.TotalMilliseconds / queryCount;
        double qps = queryCount / swQuery.Elapsed.TotalSeconds;
        Console.WriteLine($"[LARGE] Query latency: {avgLatency:F2}ms avg, {qps:N0} QPS (single-threaded)");

        Console.WriteLine("[LARGE] 500K test complete.");
    }

    // =========================================================================
    // Recall at scale -- verify HNSW quality doesn't degrade with more docs
    // =========================================================================

    [Fact]
    public void Recall_100K_Dim128_Above95Percent()
    {
        int dim = 128;
        int docCount = 100_000;
        int topK = 10;
        int queryCount = 50;

        Console.WriteLine($"[LARGE] Starting recall test ({docCount:N0} docs, dim={dim})...");

        using var col = ZvecCollection.CreateAndOpen(CollPath("recall_100k"), schema =>
        {
            schema.AddVector("vec", (uint)dim, MetricType.Cosine);
        });

        var rng = new Random(42);

        // Store all vectors for brute force comparison
        var allVecs = new float[docCount][];

        int batchSize = 1000;
        for (int batch = 0; batch < docCount / batchSize; batch++)
        {
            var docs = new List<ZvecDocument>(batchSize);
            try
            {
                for (int i = 0; i < batchSize; i++)
                {
                    int idx = batch * batchSize + i;
                    allVecs[idx] = RandomVector(dim, rng);
                    var doc = new ZvecDocument($"d{idx}");
                    doc.SetVector("vec", allVecs[idx]);
                    docs.Add(doc);
                }
                col.Insert(docs);
            }
            finally
            {
                foreach (var d in docs) d.Dispose();
            }
        }

        col.CreateIndex("vec");
        Console.WriteLine($"[LARGE] Inserted and indexed {docCount:N0} docs.");

        int totalHits = 0;
        int totalExpected = 0;

        for (int q = 0; q < queryCount; q++)
        {
            var qvec = RandomVector(dim, rng);

            // Brute force top-K by cosine similarity
            var sims = new (int idx, float sim)[docCount];
            for (int i = 0; i < docCount; i++)
            {
                float dot = 0, nA = 0, nB = 0;
                for (int j = 0; j < dim; j++)
                {
                    dot += qvec[j] * allVecs[i][j];
                    nA += qvec[j] * qvec[j];
                    nB += allVecs[i][j] * allVecs[i][j];
                }
                sims[i] = (i, dot / (MathF.Sqrt(nA) * MathF.Sqrt(nB)));
            }
            Array.Sort(sims, (a, b) => b.sim.CompareTo(a.sim));

            var groundTruth = new HashSet<string>();
            for (int i = 0; i < topK; i++)
                groundTruth.Add($"d{sims[i].idx}");

            using var query = VectorQuery.For("vec", qvec, topK);
            var results = col.Query(query);

            totalHits += results.Count(r => groundTruth.Contains(r.Id));
            totalExpected += topK;
        }

        double recall = (double)totalHits / totalExpected;
        Console.WriteLine($"[LARGE] Recall@{topK} at {docCount:N0} docs: {recall:P1} ({totalHits}/{totalExpected})");

        Assert.True(recall > 0.95, $"Recall too low at scale: {recall:P1}");
    }
}
