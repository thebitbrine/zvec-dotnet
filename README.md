# Zvec.NET

.NET bindings for [Zvec](https://github.com/alibaba/zvec), an embedded vector database from Alibaba. Provides in-process vector similarity search with HNSW/IVF indexing, disk persistence, and no server dependency.

Wraps the Zvec C API (`c_api.h`) via P/Invoke. Targets .NET 8.0+.

## Installation

```
dotnet add package Zvec
```

The package includes pre-built native binaries for Windows x64. Linux and macOS support requires building the native library from source (see below).

## Usage

```csharp
using Zvec;
using Zvec.Native;

// Create a collection with a vector field and scalar fields
using var collection = ZvecCollection.CreateAndOpen("./my_collection", schema =>
{
    schema.AddVector("embedding", dimensions: 1024, metric: MetricType.Cosine);
    schema.AddScalar("title", DataType.String);
    schema.AddScalar("year", DataType.Int32);
});

// Insert documents
using (var doc = new ZvecDocument("doc_1"))
{
    doc.SetVector("embedding", myEmbeddingArray);  // float[]
    doc.SetString("title", "Example document");
    doc.SetInt32("year", 2026);
    collection.Insert(doc);
}

// Register the index and build the HNSW graph
collection.CreateIndex("embedding");
collection.Optimize();  // actually builds the graph; skip for small collections

// Query
using var query = VectorQuery.For("embedding", queryVector, topK: 10);
IReadOnlyList<SearchResult> results = collection.Query(query);

foreach (var result in results)
    Console.WriteLine($"{result.Id}: distance={result.Score}");
```

Documents must be disposed by the caller after insert (insert borrows, does not take ownership).

## Score semantics

Scores are **distances**, not similarities. Lower values indicate more similar vectors.

| Metric | Identical vectors | Opposite vectors |
|--------|------------------|-----------------|
| Cosine | 0.0 | ~2.0 |
| L2 | 0.0 | depends on magnitude |
| Inner Product | 0.0 | depends on magnitude |

Results are returned sorted by ascending distance (most similar first).

## Supported field types

Scalar types: `String`, `Bool`, `Int32`, `Int64`, `UInt32`, `UInt64`, `Float`, `Double`

Vector types: `VectorFp32` (default), `VectorFp16`, `VectorFp64`, `VectorInt8`

Index types: `Hnsw` (default), `Ivf`, `Flat`

Metric types: `Cosine` (default), `L2`, `InnerProduct`

## API reference

### ZvecCollection

```csharp
// Create new collection
ZvecCollection.CreateAndOpen(string path, Action<ZvecSchema> configure)

// Open existing collection
ZvecCollection.Open(string path)

// Write operations (documents are borrowed, not consumed)
void Insert(ZvecDocument doc)
(nuint success, nuint errors) Insert(IReadOnlyList<ZvecDocument> docs)
void Upsert(ZvecDocument doc)
void Update(ZvecDocument doc)
void Delete(string primaryKey)

// Index and query
void CreateIndex(string fieldName, IndexType type, MetricType metric)
void Optimize()   // builds the HNSW graph, merges segments -- call after bulk inserts
IReadOnlyList<SearchResult> Query(VectorQuery query)

// Lifecycle
void Flush()
void Close()      // also called by Dispose()
```

### ZvecDocument

```csharp
new ZvecDocument(string primaryKey)

void SetVector(string field, float[] vector)
void SetString(string field, string value)
void SetInt32(string field, int value)
void SetInt64(string field, long value)
void SetFloat(string field, float value)
void SetDouble(string field, double value)
void SetBool(string field, bool value)
```

### VectorQuery

```csharp
VectorQuery.For(string fieldName, float[] vector, int topK)
```

## Building the native library

The managed code depends on `zvec_c_api.dll` (Windows), `libzvec_c_api.so` (Linux), or `libzvec_c_api.dylib` (macOS).

Pre-built binaries can be extracted from the [Zvec Python package](https://pypi.org/project/zvec/) (`bin/zvec_c_api.dll` and `bin/zvec.dll` inside the wheel).

To build from source:

```
cd native
# Windows (requires Visual Studio 2022, CMake 3.13+)
powershell -File build-native.ps1

# Linux/macOS (requires gcc/clang, CMake 3.13+)
bash build-native.sh
```

Place the output in `runtimes/{rid}/native/` where `{rid}` is `win-x64`, `linux-x64`, or `osx-arm64`.

## Running tests

```
dotnet test
```

Tests require the native library for the current platform to be present in the runtimes directory. The test suite includes unit tests, integration tests, concurrency tests, and performance benchmarks.

## Project structure

```
src/Zvec.Native/    P/Invoke declarations, SafeHandles, enums
src/Zvec/           Public API (ZvecCollection, ZvecDocument, VectorQuery, etc.)
tests/Zvec.Tests/   xUnit test suite
samples/            Working examples
native/             Build scripts for the native library
```

## Compatibility

- .NET 8.0 or later
- Zvec native library v0.4.0
- Windows x64 (pre-built), Linux x64, macOS ARM64 (build from source)

## License

Apache 2.0, matching the Zvec upstream license.
