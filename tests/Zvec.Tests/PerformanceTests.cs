using System.Diagnostics;

namespace Zvec.Tests;

[Collection("Zvec")]
public class PerformanceTests : IDisposable
{
    private readonly string _tempDir;

    public PerformanceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_perf_" + Guid.NewGuid().ToString("N")[..8]);
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
    // Insert throughput
    // =========================================================================

    [Theory]
    [InlineData(128, 10_000)]
    [InlineData(256, 10_000)]
    [InlineData(768, 5_000)]
    [InlineData(1024, 5_000)]
    public void InsertThroughput(int dim, int docCount)
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath($"perf_ins_{dim}_{docCount}"), schema =>
        {
            schema.AddVector("vec", (uint)dim, MetricType.Cosine);
        });

        var rng = new Random(42);

        // Pre-generate all vectors to exclude generation time from measurement
        var vectors = new float[docCount][];
        for (int i = 0; i < docCount; i++)
            vectors[i] = RandomVector(dim, rng);

        var sw = Stopwatch.StartNew();

        // Insert in batches of 100
        int batchSize = 100;
        for (int batch = 0; batch < docCount / batchSize; batch++)
        {
            var docs = new List<ZvecDocument>(batchSize);
            try
            {
                for (int i = 0; i < batchSize; i++)
                {
                    int idx = batch * batchSize + i;
                    var doc = new ZvecDocument($"doc_{idx}");
                    doc.SetVector("vec", vectors[idx]);
                    docs.Add(doc);
                }
                col.Insert(docs);
            }
            finally
            {
                foreach (var d in docs) d.Dispose();
            }
        }

        sw.Stop();
        double docsPerSec = docCount / sw.Elapsed.TotalSeconds;

        // Output for visibility
        Console.WriteLine($"[PERF] Insert: {docCount} docs, dim={dim}, {sw.Elapsed.TotalMilliseconds:F0}ms, {docsPerSec:F0} docs/sec");

        // Sanity: should be at least 1000 docs/sec for any dimension
        Assert.True(docsPerSec > 1000, $"Insert too slow: {docsPerSec:F0} docs/sec (dim={dim})");
    }

    // =========================================================================
    // Index build time
    // =========================================================================

    [Theory]
    [InlineData(128, 50_000)]
    [InlineData(768, 10_000)]
    public void IndexBuildTime(int dim, int docCount)
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath($"perf_idx_{dim}_{docCount}"), schema =>
        {
            schema.AddVector("vec", (uint)dim, MetricType.Cosine);
        });

        var rng = new Random(42);

        // Insert docs first
        int batchSize = 100;
        for (int batch = 0; batch < docCount / batchSize; batch++)
        {
            var docs = new List<ZvecDocument>(batchSize);
            try
            {
                for (int i = 0; i < batchSize; i++)
                {
                    int idx = batch * batchSize + i;
                    var doc = new ZvecDocument($"doc_{idx}");
                    doc.SetVector("vec", RandomVector(dim, rng));
                    docs.Add(doc);
                }
                col.Insert(docs);
            }
            finally
            {
                foreach (var d in docs) d.Dispose();
            }
        }

        var sw = Stopwatch.StartNew();
        col.CreateIndex("vec");
        sw.Stop();

        Console.WriteLine($"[PERF] Index build: {docCount} docs, dim={dim}, {sw.Elapsed.TotalMilliseconds:F0}ms");

        // Index build should complete in reasonable time
        Assert.True(sw.Elapsed.TotalSeconds < 120, $"Index build too slow: {sw.Elapsed.TotalSeconds:F1}s");
    }

    // =========================================================================
    // Query latency (single query, hot path)
    // =========================================================================

    [Theory]
    [InlineData(128, 10_000, 10)]
    [InlineData(128, 50_000, 10)]
    [InlineData(768, 10_000, 10)]
    public void QueryLatency(int dim, int docCount, int topK)
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath($"perf_q_{dim}_{docCount}"), schema =>
        {
            schema.AddVector("vec", (uint)dim, MetricType.Cosine);
        });

        var rng = new Random(42);

        // Insert docs
        int batchSize = 100;
        for (int batch = 0; batch < docCount / batchSize; batch++)
        {
            var docs = new List<ZvecDocument>(batchSize);
            try
            {
                for (int i = 0; i < batchSize; i++)
                {
                    int idx = batch * batchSize + i;
                    var doc = new ZvecDocument($"doc_{idx}");
                    doc.SetVector("vec", RandomVector(dim, rng));
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

        // Warmup query
        var warmupVec = RandomVector(dim, rng);
        using (var warmup = VectorQuery.For("vec", warmupVec, topK))
            col.Query(warmup);

        // Measure N queries
        int queryCount = 100;
        var queryVecs = new float[queryCount][];
        for (int i = 0; i < queryCount; i++)
            queryVecs[i] = RandomVector(dim, rng);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < queryCount; i++)
        {
            using var query = VectorQuery.For("vec", queryVecs[i], topK);
            var results = col.Query(query);
            Assert.Equal(topK, results.Count);
        }
        sw.Stop();

        double avgLatencyMs = sw.Elapsed.TotalMilliseconds / queryCount;
        double qps = queryCount / sw.Elapsed.TotalSeconds;

        Console.WriteLine($"[PERF] Query: {docCount} docs, dim={dim}, topK={topK}, avg={avgLatencyMs:F2}ms, {qps:F0} QPS");

        // Single query should be under 100ms for these sizes
        Assert.True(avgLatencyMs < 100, $"Query too slow: {avgLatencyMs:F2}ms avg (dim={dim}, docs={docCount})");
    }

    // =========================================================================
    // Query throughput (concurrent)
    // =========================================================================

    [Fact]
    public void QueryThroughput_Concurrent()
    {
        int dim = 128;
        int docCount = 50_000;
        int topK = 10;
        int threadCount = Environment.ProcessorCount;
        int queriesPerThread = 50;

        using var col = ZvecCollection.CreateAndOpen(CollPath("perf_qps"), schema =>
        {
            schema.AddVector("vec", (uint)dim, MetricType.Cosine);
        });

        var rng = new Random(42);

        // Insert docs
        int batchSize = 100;
        for (int batch = 0; batch < docCount / batchSize; batch++)
        {
            var docs = new List<ZvecDocument>(batchSize);
            try
            {
                for (int i = 0; i < batchSize; i++)
                {
                    int idx = batch * batchSize + i;
                    var doc = new ZvecDocument($"doc_{idx}");
                    doc.SetVector("vec", RandomVector(dim, rng));
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

        // Pre-generate query vectors per thread
        var threadVecs = new float[threadCount][][];
        for (int t = 0; t < threadCount; t++)
        {
            var tRng = new Random(42 + t);
            threadVecs[t] = new float[queriesPerThread][];
            for (int q = 0; q < queriesPerThread; q++)
                threadVecs[t][q] = RandomVector(dim, tRng);
        }

        var threads = new Thread[threadCount];
        var errors = new Exception?[threadCount];

        var sw = Stopwatch.StartNew();
        for (int t = 0; t < threadCount; t++)
        {
            int threadIdx = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    for (int q = 0; q < queriesPerThread; q++)
                    {
                        using var query = VectorQuery.For("vec", threadVecs[threadIdx][q], topK);
                        var results = col.Query(query);
                        if (results.Count != topK)
                            throw new Exception($"Expected {topK} results, got {results.Count}");
                    }
                }
                catch (Exception ex) { errors[threadIdx] = ex; }
            });
            threads[t].Start();
        }

        foreach (var thread in threads) thread.Join();
        sw.Stop();

        foreach (var err in errors) Assert.Null(err);

        int totalQueries = threadCount * queriesPerThread;
        double qps = totalQueries / sw.Elapsed.TotalSeconds;

        Console.WriteLine($"[PERF] Concurrent query: {docCount} docs, dim={dim}, {threadCount} threads x {queriesPerThread} queries, {sw.Elapsed.TotalMilliseconds:F0}ms total, {qps:F0} QPS");

        // Should handle at least 100 QPS with these parameters
        Assert.True(qps > 100, $"Concurrent QPS too low: {qps:F0}");
    }

    // =========================================================================
    // Recall accuracy (sanity check that HNSW returns reasonable results)
    // =========================================================================

    [Fact]
    public void RecallAccuracy_Top10_Above90Percent()
    {
        int dim = 64;
        int docCount = 1000;
        int topK = 10;
        int queryCount = 20;

        using var col = ZvecCollection.CreateAndOpen(CollPath("perf_recall"), schema =>
        {
            schema.AddVector("vec", (uint)dim, MetricType.Cosine);
        });

        var rng = new Random(42);
        var allVecs = new float[docCount][];

        // Insert docs
        for (int i = 0; i < docCount; i++)
        {
            allVecs[i] = RandomVector(dim, rng);
            using var doc = new ZvecDocument($"doc_{i}");
            doc.SetVector("vec", allVecs[i]);
            col.Insert(doc);
        }

        col.CreateIndex("vec");

        // For each query, compute brute-force top-K and compare with HNSW results
        int totalHits = 0;
        int totalExpected = 0;

        for (int q = 0; q < queryCount; q++)
        {
            var qvec = RandomVector(dim, rng);

            // Brute-force: compute cosine similarity with all docs
            var similarities = new (int idx, float sim)[docCount];
            for (int i = 0; i < docCount; i++)
            {
                float dot = 0, normA = 0, normB = 0;
                for (int j = 0; j < dim; j++)
                {
                    dot += qvec[j] * allVecs[i][j];
                    normA += qvec[j] * qvec[j];
                    normB += allVecs[i][j] * allVecs[i][j];
                }
                float sim = dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
                similarities[i] = (i, sim);
            }
            Array.Sort(similarities, (a, b) => b.sim.CompareTo(a.sim));

            var bruteForceTop = new HashSet<string>();
            for (int i = 0; i < topK; i++)
                bruteForceTop.Add($"doc_{similarities[i].idx}");

            // HNSW query
            using var query = VectorQuery.For("vec", qvec, topK);
            var results = col.Query(query);

            int hits = results.Count(r => bruteForceTop.Contains(r.Id));
            totalHits += hits;
            totalExpected += topK;
        }

        double recall = (double)totalHits / totalExpected;
        Console.WriteLine($"[PERF] Recall@{topK}: {recall:P1} ({totalHits}/{totalExpected})");

        // HNSW should achieve > 90% recall with default params
        Assert.True(recall > 0.9, $"Recall too low: {recall:P1}");
    }
}
