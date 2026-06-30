# Zvec.NET

.NET bindings for [Zvec](https://github.com/alibaba/zvec), an embedded vector database from Alibaba. Provides in-process vector similarity search with HNSW/IVF indexing, disk persistence, and no server dependency.

Wraps the Zvec C API (`c_api.h`) via P/Invoke. Targets .NET 8.0+.

## Installation

```
dotnet add package Zvec
```

The NuGet package includes pre-built native binaries for Windows x64, Linux x64, and macOS ARM64.

## Quick start

```csharp
using Zvec;
using Zvec.Native;

// Create a collection
using var collection = ZvecCollection.CreateAndOpen("./my_collection", schema =>
{
    schema.AddVector("embedding", dimensions: 1024, metric: MetricType.Cosine);
    schema.AddScalar("title", DataType.String);
    schema.AddScalar("year", DataType.Int32);
});

// Insert
using (var doc = new ZvecDocument("doc_1"))
{
    doc.SetVector("embedding", myEmbeddingArray);  // float[]
    doc.SetString("title", "Example document");
    doc.SetInt32("year", 2026);
    collection.Insert(doc);
}

// Build the index
collection.CreateIndex("embedding");
collection.Optimize();  // builds the HNSW graph; skip for small collections

// Query
using var query = VectorQuery.For("embedding", queryVector, topK: 10);
var results = collection.Query(query);

foreach (var result in results)
    Console.WriteLine($"{result.Id}: distance={result.Score}");
```

Documents must be disposed by the caller after insert (insert borrows, does not take ownership).

## Hybrid search (multi-vector + reranking)

Search across multiple vector fields and combine results with RRF or weighted fusion.

```csharp
using var collection = ZvecCollection.CreateAndOpen("./hybrid", schema =>
{
    schema.AddVector("dense_vec", 1024, MetricType.Cosine);
    schema.AddSparseVector("sparse_vec");
    schema.AddScalar("title", DataType.String);
});

// Insert with dense + sparse vectors
using (var doc = new ZvecDocument("doc_1"))
{
    doc.SetVector("dense_vec", denseEmbedding);
    doc.SetSparseVector("sparse_vec", new Dictionary<uint, float>
    {
        [42] = 0.8f, [99] = 0.3f, [1024] = 1.5f,
    });
    doc.SetString("title", "Example");
    collection.Insert(doc);
}

collection.CreateIndex("dense_vec");
collection.CreateIndex("sparse_vec");

// Multi-vector query with RRF reranking
using var query = new MultiQuery(topK: 10)
    .AddSubQuery("dense_vec", queryDenseVec)
    .AddSparseSubQuery("sparse_vec", querySparseVec)
    .WithRrfReranker(rankConstant: 60);

var results = collection.Query(query);
```

Weighted fusion is also available:

```csharp
// Weights are applied in sub-query order
using var query = new MultiQuery(topK: 10)
    .AddSubQuery("dense_vec", queryDenseVec)
    .AddSubQuery("content_vec", queryContentVec)
    .WithWeightedReranker(0.7, 0.3);
```

## Filtered search

```csharp
// Scalar field filtering
using var query = VectorQuery.For("embedding", queryVec, topK: 10)
    .WithFilter("year >= 2020 AND category = 'science'");

// Array field with CONTAIN_ANY
using var collection = ZvecCollection.CreateAndOpen("./filtered", schema =>
{
    schema.AddVector("vec", 1024, MetricType.Cosine);
    schema.AddArray("concepts", DataType.ArrayString, indexed: true);
});

using (var doc = new ZvecDocument("doc_1"))
{
    doc.SetVector("vec", embedding);
    doc.SetStringArray("concepts", new[] { "physics", "quantum", "entanglement" });
    collection.Insert(doc);
}

using var query = VectorQuery.For("vec", queryVec, topK: 10)
    .WithFilter("concepts CONTAIN_ANY ('physics', 'chemistry')");
```

## Index tuning

```csharp
// HNSW with custom parameters
collection.CreateHnswIndex("embedding",
    metric: MetricType.Cosine,
    m: 32,                                  // graph connectivity
    efConstruction: 400,                    // build exploration factor
    quantization: QuantizationType.Int8);   // reduced memory

// Other index types
collection.CreateIndex("embedding", IndexType.Flat, MetricType.L2);
collection.CreateIndex("embedding", IndexType.Ivf, MetricType.Cosine);
```

## Schema evolution

```csharp
collection.AddColumn("priority", DataType.Int32, nullable: true);
collection.DropColumn("old_field");
collection.RenameColumn("old_name", "new_name");
```

Only numeric types (int32, int64, uint32, uint64, float, double) are supported for add/drop/rename.

## Score semantics

Scores are **distances**, not similarities. Lower = more similar.

| Metric | Identical vectors | Opposite vectors |
|--------|------------------|-----------------|
| Cosine | 0.0 | ~2.0 |
| L2 | 0.0 | depends on magnitude |
| Inner Product | 0.0 | depends on magnitude |

## Supported types

**Scalar:** `String`, `Bool`, `Int32`, `Int64`, `UInt32`, `UInt64`, `Float`, `Double`

**Vector:** `VectorFp32` (default), `VectorFp16`, `VectorFp64`, `VectorInt8`, `SparseVectorFp32`

**Array:** `ArrayString`, `ArrayInt32`, `ArrayInt64`, `ArrayFloat`, `ArrayDouble`

**Index:** `Hnsw` (default), `Ivf`, `Flat`, `Invert` (for array fields)

**Metric:** `Cosine` (default), `L2`, `InnerProduct`

**Quantization:** `Fp16`, `Int8`, `Int4`

## API reference

### ZvecCollection

```csharp
// Lifecycle
ZvecCollection.CreateAndOpen(string path, Action<ZvecSchema> configure)
ZvecCollection.Open(string path)
void Flush()
void Close()

// CRUD (documents are borrowed, not consumed)
void Insert(ZvecDocument doc)
(nuint success, nuint errors) Insert(IReadOnlyList<ZvecDocument> docs)
void Upsert(ZvecDocument doc)
void Update(ZvecDocument doc)
void Delete(string primaryKey)

// Index management
void CreateIndex(string fieldName, IndexType type, MetricType metric)
void CreateIndex(string fieldName, IndexType type, MetricType metric, QuantizationType quantization)
void CreateHnswIndex(string fieldName, MetricType metric, int m, int efConstruction, QuantizationType quantization)
void DropIndex(string fieldName)
void Optimize()

// Query
IReadOnlyList<SearchResult> Query(VectorQuery query)
IReadOnlyList<SearchResult> Query(MultiQuery query)

// Schema evolution
void AddColumn(string name, DataType dataType, bool nullable, string defaultExpression)
void DropColumn(string columnName)
void RenameColumn(string oldName, string newName)
```

### ZvecDocument

```csharp
new ZvecDocument(string primaryKey)

// Scalar fields
void SetString(string field, string value)
void SetInt32(string field, int value)
void SetInt64(string field, long value)
void SetFloat(string field, float value)
void SetDouble(string field, double value)
void SetBool(string field, bool value)

// Vector fields
void SetVector(string field, float[] vector)
void SetSparseVector(string field, Dictionary<uint, float> sparseVector)

// Array fields
void SetStringArray(string field, string[] values)
void SetInt32Array(string field, int[] values)
void SetInt64Array(string field, long[] values)
void SetFloatArray(string field, float[] values)
```

### VectorQuery

```csharp
VectorQuery.For(string fieldName, float[] vector, int topK)
VectorQuery WithFilter(string filterExpression)
```

### MultiQuery

```csharp
new MultiQuery(int topK)
MultiQuery AddSubQuery(string fieldName, float[] vector, int numCandidates)
MultiQuery AddSparseSubQuery(string fieldName, Dictionary<uint, float> sparseVector, int numCandidates)
MultiQuery WithRrfReranker(int rankConstant = 60)
MultiQuery WithWeightedReranker(params double[] weights)  // weights in sub-query order
MultiQuery WithFilter(string filter)
```

### ZvecSchema

```csharp
void AddVector(string name, uint dimensions, MetricType metric, IndexType indexType)
void AddSparseVector(string name, MetricType metric, IndexType indexType)
void AddScalar(string name, DataType dataType, bool nullable)
void AddArray(string name, DataType dataType, bool indexed, bool nullable)
```

## Building the native library

Pre-built binaries can be extracted from the [Zvec Python package](https://pypi.org/project/zvec/) (`bin/` directory inside the wheel). For hybrid search features (multi-query, reranking), build from the `main` branch:

```
cd native
# Windows (requires Visual Studio 2022, CMake 3.13+)
powershell -File build-native.ps1 -Tag main

# Linux/macOS (requires gcc/clang, CMake 3.13+)
bash build-native.sh main
```

Place the output in `runtimes/{rid}/native/` where `{rid}` is `win-x64`, `linux-x64`, or `osx-arm64`.

## Running tests

```
dotnet test
```

The test suite has 220 tests covering correctness, CRUD, search, hybrid search, filtering, concurrency, and performance benchmarks.

## Compatibility

- .NET 8.0 or later
- Zvec native library v0.4.0+ (v0.5.0+ for hybrid search)
- Windows x64, Linux x64, macOS ARM64

## License

Apache 2.0, matching the Zvec upstream license.
