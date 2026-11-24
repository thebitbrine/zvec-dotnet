# Zvec.NET

.NET bindings for [Zvec](https://github.com/alibaba/zvec), Alibaba's embedded vector database. In-process vector search, full-text search and hybrid search with disk persistence and no server to run.

Wraps the Zvec C API (`c_api.h`) via P/Invoke. Targets .NET 8.0+. Package version tracks the upstream Zvec release it ships (0.8.1 = Zvec v0.8.1).

## Installation

```
dotnet add package Zvec
```

Native libraries come along through the `Zvec.Native` dependency for:

| RID | Notes |
|-----|-------|
| `win-x64` | |
| `linux-x64` | glibc 2.27+ |
| `linux-arm64` | glibc 2.27+ |
| `osx-arm64` | |

They are the unmodified prebuilt libraries from the upstream Zvec release.

## Quick start

```csharp
using Zvec;
using Zvec.Native;

using var collection = ZvecCollection.CreateAndOpen("./my_collection", schema =>
{
    schema.AddVector("embedding", dimensions: 1024, metric: MetricType.Cosine);
    schema.AddScalar("title", DataType.String);
    schema.AddScalar("year", DataType.Int32);
});

using (var doc = new ZvecDocument("doc_1"))
{
    doc.SetVector("embedding", myEmbeddingArray);  // float[]
    doc.SetString("title", "Example document");
    doc.SetInt32("year", 2026);
    collection.Insert(doc);
}

collection.Optimize();  // builds the HNSW graph; skip for small collections

using var query = VectorQuery.For("embedding", queryVector, topK: 10);
foreach (var result in collection.Query(query))
    Console.WriteLine($"{result.Id}: distance={result.Score}");
```

Documents must be disposed by the caller after insert (insert borrows, does not take ownership).

## Hybrid search (vector + keyword)

Add a text field with a full-text index, then run a vector sub-query and a keyword sub-query in one `MultiQuery`. Zvec fuses the two result lists.

```csharp
using var collection = ZvecCollection.CreateAndOpen("./kb", schema =>
{
    schema.AddVector("embedding", 1024, MetricType.Cosine);
    schema.AddText("body");                       // full-text indexed
});

using (var doc = new ZvecDocument("doc_1"))
{
    doc.SetVector("embedding", embedding);
    doc.SetString("body", "Restore hangs after manifest load, set LEGACY_FETCHER=1");
    collection.Insert(doc);
}

using var query = new MultiQuery(topK: 10)
    .AddSubQuery("embedding", queryEmbedding, numCandidates: 50)   // semantic
    .AddFtsSubQuery("body", "LEGACY_FETCHER", numCandidates: 50)   // exact keyword
    .WithRrfReranker();

foreach (var hit in collection.Query(query))
    Console.WriteLine($"{hit.Id}: {hit.Score}");   // higher = better here
```

`WithWeightedReranker(0.7, 0.3)` weighs the sub-queries instead (weights in sub-query order), and `WithFilter("year >= 2025")` applies to the whole query. A `MultiQuery` needs at least two sub-queries. Sparse vectors work the same way through `AddSparseSubQuery`.

## Full-text search

```csharp
// natural language, terms OR-ed by default
using var q1 = VectorQuery.ForFts("body", topK: 10).WithFtsMatch("restore manifest");

// every term required
using var q2 = VectorQuery.ForFts("body", topK: 10).WithFtsOperator("AND").WithFtsMatch("restore manifest");

// query expression
using var q3 = VectorQuery.ForFts("body", topK: 10).WithFts("checkpoint AND (nfs OR mount)");
```

Tokenizer and filters are set per field:

```csharp
schema.AddText("body");                                             // standard tokenizer
schema.AddText("code", tokenizerName: "ngram", filters: new[] { "lowercase" },
    extraParams: "{\"ngram_min\":3,\"ngram_max\":3}");               // substring matching
schema.AddText("notes", filters: new[] { "lowercase", "stemmer" },
    extraParams: "{\"stemmer_lang\":\"english\"}");
```

Tokenizers: `standard`, `ngram`, `jieba`, `whitespace`. Filters: `lowercase`, `ascii_folding`, `stemmer`.

An existing string column can get an index later with `collection.CreateFtsIndex("body")`. Rows already in the collection are indexed too.

## Filtered search

```csharp
using var query = VectorQuery.For("embedding", queryVec, topK: 10)
    .WithFilter("year >= 2020 AND category = 'science'");

// array field with CONTAIN_ANY (needs AddArray(..., indexed: true))
using var query2 = VectorQuery.For("vec", queryVec, topK: 10)
    .WithFilter("concepts CONTAIN_ANY ('physics', 'chemistry')");
```

## Grouped search

Best matches per distinct value of a scalar field, for example the top chunks per source document:

```csharp
using var query = GroupByQuery.For("embedding", queryVec, groupByField: "doc_id",
    groupCount: 5, topKPerGroup: 2);

foreach (var group in collection.Query(query))
    Console.WriteLine($"{group.Value}: {string.Join(", ", group.Results.Select(r => r.Id))}");
```

## Reading documents back

```csharp
// by primary key
using (var fetched = collection.Fetch(new[] { "doc_1", "doc_2" }))
    foreach (var doc in fetched.Docs)
        Console.WriteLine($"{doc.Pk}: {doc.GetString("title")}");

// every document (snapshot); each doc is only readable until the next one
foreach (var doc in collection.Scan(outputFields: new[] { "title" }))
    Console.WriteLine($"{doc.Pk}: {doc.GetString("title")}");
```

Zvec refuses schema changes and `Optimize()` while a scan is open, so finish or break out of the `foreach` before those.

## Index tuning

```csharp
collection.CreateHnswIndex("embedding",
    metric: MetricType.Cosine,
    m: 32,                                  // graph connectivity
    efConstruction: 400,                    // build exploration factor
    quantization: QuantizationType.Int8);   // reduced memory

// search-time exploration factor (zvec default 40)
using var query = VectorQuery.For("embedding", queryVec, topK: 10).WithEf(200);

collection.CreateIndex("embedding", IndexType.Flat, MetricType.L2);
collection.CreateIndex("embedding", IndexType.Ivf, MetricType.Cosine);
```

`collection.GetStats()` returns the document count and how complete each index is.

## Other operations

```csharp
collection.DeleteByFilter("year < 2020");

using var readOnly = ZvecCollection.Open("./my_collection", readOnly: true);

collection.AddColumn("priority", DataType.Int32, nullable: true);
collection.DropColumn("old_field");
collection.RenameColumn("old_name", "new_name");
```

Add/drop/rename column only supports numeric types (int32, int64, uint32, uint64, float, double).

## Score semantics

The meaning of `Score` depends on the query:

| Query | Score | Best |
|-------|-------|------|
| Vector, Cosine | distance, 0.0 = identical, ~2.0 = opposite | lowest |
| Vector, L2 | distance, 0.0 = identical | lowest |
| Full-text (`ForFts`) | BM25 relevance | highest |
| `MultiQuery` | fused rank score | highest |

Results always come back best first.

## Supported types

**Scalar:** `String`, `Bool`, `Int32`, `Int64`, `UInt32`, `UInt64`, `Float`, `Double`

**Vector:** `VectorFp32` (the only one with typed setters), `SparseVectorFp32`

**Array:** `ArrayString`, `ArrayInt32`, `ArrayInt64`, `ArrayFloat`

**Index:** `Hnsw` (default), `Ivf`, `Flat`, `Invert` (array fields), `Fts` (text fields)

**Metric:** `Cosine` (default), `L2`, `InnerProduct`

**Quantization:** `Fp16`, `Int8`, `Int4`

The `IndexType` and `QuantizationType` enums also list Zvec's newer kinds (`HnswRabitq`, `IvfRabitq`, `DiskAnn`, `Vamana`, `Rabitq`, `Pq`). They can be passed to `CreateIndex` and run with Zvec's default parameters; their tuning knobs are not wrapped and they are not covered by this project's tests.

## API reference

### ZvecCollection

```csharp
// Lifecycle
ZvecCollection.CreateAndOpen(string path, Action<ZvecSchema> configure)
ZvecCollection.Open(string path)
ZvecCollection.Open(string path, bool readOnly)
void Flush()
void Close()
CollectionStats GetStats()

// Writes (documents are borrowed, not consumed)
void Insert(ZvecDocument doc)
(nuint success, nuint errors) Insert(IReadOnlyList<ZvecDocument> docs)
void Upsert(ZvecDocument doc)
void Update(ZvecDocument doc)
void Delete(string primaryKey)
void DeleteByFilter(string filterExpression)

// Reads
IReadOnlyList<SearchResult> Query(VectorQuery query)
IReadOnlyList<SearchResult> Query(MultiQuery query)
IReadOnlyList<SearchGroup> Query(GroupByQuery query)
FetchResult Fetch(IReadOnlyList<string> primaryKeys, IReadOnlyList<string>? outputFields, bool includeVector)
IEnumerable<FetchedDoc> Scan(IReadOnlyList<string>? outputFields, bool includeVector)

// Index management
void CreateIndex(string fieldName, IndexType type, MetricType metric)
void CreateIndex(string fieldName, IndexType type, MetricType metric, QuantizationType quantization)
void CreateHnswIndex(string fieldName, MetricType metric, int m, int efConstruction, QuantizationType quantization)
void CreateFtsIndex(string fieldName, string? tokenizerName, IReadOnlyList<string>? filters, string? extraParams)
void DropIndex(string fieldName)
void Optimize()

// Schema evolution
void AddColumn(string name, DataType dataType, bool nullable, string defaultExpression)
void DropColumn(string columnName)
void RenameColumn(string oldName, string newName)
```

### ZvecSchema

```csharp
void AddVector(string name, uint dimensions, MetricType metric, IndexType indexType)
void AddSparseVector(string name, MetricType metric, IndexType indexType)
void AddText(string name, string? tokenizerName, IReadOnlyList<string>? filters, string? extraParams, bool nullable)
void AddScalar(string name, DataType dataType, bool nullable)
void AddArray(string name, DataType dataType, bool indexed, bool nullable)
```

### ZvecDocument

```csharp
new ZvecDocument(string primaryKey)

void SetString(string field, string value)
void SetInt32(string field, int value)
void SetInt64(string field, long value)
void SetFloat(string field, float value)
void SetDouble(string field, double value)
void SetBool(string field, bool value)

void SetVector(string field, float[] vector)
void SetSparseVector(string field, Dictionary<uint, float> sparseVector)

void SetStringArray(string field, string[] values)
void SetInt32Array(string field, int[] values)
void SetInt64Array(string field, long[] values)
void SetFloatArray(string field, float[] values)
```

### VectorQuery

```csharp
VectorQuery.For(string fieldName, float[] vector, int topK)
VectorQuery.ForFts(string fieldName, int topK)
VectorQuery WithFilter(string filterExpression)
VectorQuery WithEf(int ef)
VectorQuery WithFtsMatch(string matchString)
VectorQuery WithFts(string queryString)
VectorQuery WithFtsOperator(string defaultOperator)
```

### MultiQuery

```csharp
new MultiQuery(int topK)
MultiQuery AddSubQuery(string fieldName, float[] vector, int numCandidates)
MultiQuery AddSparseSubQuery(string fieldName, Dictionary<uint, float> sparseVector, int numCandidates)
MultiQuery AddFtsSubQuery(string fieldName, string text, int numCandidates, bool isExpression, string? defaultOperator)
MultiQuery WithRrfReranker(int rankConstant = 60)
MultiQuery WithWeightedReranker(params double[] weights)
MultiQuery WithFilter(string filter)
```

### GroupByQuery

```csharp
GroupByQuery.For(string fieldName, float[] vector, string groupByField, int groupCount, int topKPerGroup)
GroupByQuery WithFilter(string filterExpression)
```

### FetchedDoc

```csharp
string Pk
string? GetString(string field)
int? GetInt32(string field)
long? GetInt64(string field)
double? GetDouble(string field)
float[]? GetVector(string field)
```

## Upgrading from 0.5.x

No public API was removed or renamed. What changes comes from Zvec itself:

- **Full-text indexes from 0.5.x have to be rebuilt.** A collection written by the 0.5.0 package opens on 0.8.1 and vector search, filters and fetch work as before, but full-text queries fail with `FTS indexer not found` until the index is recreated once:

  ```csharp
  collection.DropIndex("body");
  collection.CreateFtsIndex("body");   // re-indexes the existing rows
  ```

- Primary keys are less restricted. 0.5.1 rejected keys longer than 64 characters and some characters such as `:`; 0.8.1 accepts those, along with `/`, spaces and non-ASCII.
- `CreateFtsIndex` on an existing column works. It used to throw `NotSupported`.
- Rows rewritten with `Upsert` or delete + insert keep their full-text entries across close and reopen. On 0.5.1 they were lost unless `Optimize()` ran before closing.
- A collection with pending WAL recovery has to be opened and closed writable once before it can be opened read-only.
- Linux libraries need glibc 2.27 or newer.

## Building and testing

The native library is not built here. `native/fetch-native.py` downloads the prebuilt one from the upstream release and checks its hash:

```
python native/fetch-native.py        # this machine's platform
dotnet build
dotnet test --filter "FullyQualifiedName!~LargeScale"
dotnet run --project samples/BasicSearch
```

Drop the filter to include the long-running large-scale benchmarks.

## License

Apache 2.0, matching the Zvec upstream license.
