# zvec-dotnet: Build Specification

## What is this

C# / .NET bindings for [Zvec](https://github.com/alibaba/zvec) -- Alibaba's embedded, in-process vector database. Zvec is written in C++ (Apache 2.0), runs without a server, persists to disk, and handles millions of vectors with HNSW/IVF indexing. It exposed a stable C-API in v0.3.0 specifically to enable multi-language bindings.

Community bindings already exist for Rust, Go, and Java. No .NET binding exists yet.

**Goal**: a NuGet package so users can `dotnet add package Zvec` and start doing vector search in-process with zero native build steps.

---

## Reference materials

Read these before writing any code:

| Resource | URL | Why |
|----------|-----|-----|
| C-API header | `https://github.com/alibaba/zvec/blob/main/src/include/zvec/c_api.h` | 3336 lines. This is THE contract. Every P/Invoke maps to a function here. |
| C-API design blog | `https://zvec.org/en/blog/2026-05-13-zvec-binding/` | Explains opaque pointer pattern, error handling, ownership semantics, module grouping. |
| Go bindings | `https://github.com/danieleugenewilliams/zvec-go` | Reference for calling order. **WARNING**: uses its own C bridge (`c/include/zvec_c.h`) with different function names than the official `c_api.h`. Do NOT copy Go bridge function names. |
| Java bindings | `https://github.com/seqeralabs/zvec-java` | Has its own `zvec_c.h` / `zvec_c.cpp` C bridge layer -- same warning as Go. |
| Rust bindings | `https://github.com/igobypenn/zvec-rust-binding` | Dual-crate pattern (sys + idiomatic). Uses bindgen against official `c_api.h` so names match. |
| Zvec quickstart | `https://zvec.org/en/docs/quickstart/` | Python API -- the C# public API should feel similar. |
| Zvec build docs | `https://zvec.org/en/docs/build/` | How to build the native library from source. |
| NuGet native packaging | `https://learn.microsoft.com/en-us/nuget/create-packages/native-files-in-net-packages` | Official guidance on shipping native DLLs via NuGet. |

---

## Architecture overview

The project follows the same pattern as SkiaSharp and SQLitePCLRaw -- proven .NET native interop architecture:

```
User code
    |
    v
[Zvec]                  <-- public, idiomatic C# API (IDisposable, SafeHandle, etc.)
    |
    v
[Zvec.Native]           <-- internal P/Invoke declarations mapping c_api.h
    |
    v
[zvec_c_api.dll/so/dylib]  <-- native binary shipped via NuGet runtime packages
```

---

## Repository structure

```
zvec-dotnet/
|
|-- .github/
|   |-- workflows/
|   |   |-- build-native.yml       # CI: builds zvec C++ on 3 platforms
|   |   |-- ci.yml                 # CI: builds managed code, runs tests, packs NuGet
|   |-- ISSUE_TEMPLATE/
|   |-- PULL_REQUEST_TEMPLATE.md
|
|-- native/
|   |-- build-native.ps1           # Windows: clone zvec, cmake, build DLL
|   |-- build-native.sh            # Linux/macOS: clone zvec, cmake, build .so/.dylib
|   |-- README.md                  # how to build native libs locally
|
|-- src/
|   |-- Zvec.Native/
|   |   |-- Zvec.Native.csproj
|   |   |-- NativeMethods.cs       # [LibraryImport] declarations (partial class)
|   |   |-- NativeMethods.Schema.cs
|   |   |-- NativeMethods.Collection.cs
|   |   |-- NativeMethods.Document.cs
|   |   |-- NativeMethods.Query.cs
|   |   |-- ErrorCodes.cs          # ZvecErrorCode enum (0-10)
|   |   |-- Handles/
|   |       |-- CollectionSchemaHandle.cs
|   |       |-- FieldSchemaHandle.cs
|   |       |-- IndexParamsHandle.cs
|   |       |-- CollectionOptionsHandle.cs
|   |       |-- CollectionHandle.cs
|   |       |-- DocumentHandle.cs
|   |       |-- VectorQueryHandle.cs
|   |
|   |-- Zvec/
|       |-- Zvec.csproj
|       |-- ZvecCollection.cs      # main entry point: open, insert, query, close
|       |-- ZvecSchema.cs          # fluent schema builder
|       |-- ZvecDocument.cs        # document builder
|       |-- VectorQuery.cs         # query builder
|       |-- SearchResult.cs        # query result model
|       |-- ZvecException.cs       # managed exception from zvec error codes
|       |-- DataType.cs            # enum: VECTOR_FP32, VECTOR_FP16, STRING, INT32, etc.
|       |-- IndexType.cs           # enum: HNSW, IVF, FLAT
|       |-- MetricType.cs          # enum: COSINE, L2, IP
|
|-- tests/
|   |-- Zvec.Tests/
|       |-- Zvec.Tests.csproj
|       |-- SchemaTests.cs
|       |-- CrudTests.cs
|       |-- SearchTests.cs
|       |-- ConcurrencyTests.cs
|       |-- ErrorHandlingTests.cs
|
|-- samples/
|   |-- BasicSearch/
|       |-- BasicSearch.csproj
|       |-- Program.cs             # minimal working example
|
|-- runtimes/                      # populated by CI, included in NuGet
|   |-- win-x64/
|   |   |-- native/
|   |       |-- zvec_c_api.dll
|   |-- linux-x64/
|   |   |-- native/
|   |       |-- libzvec_c_api.so
|   |-- osx-arm64/
|       |-- native/
|           |-- libzvec_c_api.dylib
|
|-- zvec-dotnet.sln
|-- Directory.Build.props          # shared version number, common settings
|-- README.md
|-- LICENSE                        # Apache 2.0
|-- CONTRIBUTING.md
```

---

## NuGet package strategy

Single package for v1 -- embed all platform runtimes directly in the main package. Split packages (per-RID) only if binary size becomes a problem later.

| Package name | Contents | Purpose |
|---|---|---|
| `Zvec` | Managed assemblies + all native binaries under `runtimes/` | What users install |

The `Zvec.csproj` should include:

```xml
<ItemGroup>
  <None Include="$(MSBuildThisFileDirectory)../../runtimes/**/*"
        Pack="true"
        PackagePath="runtimes/" />
</ItemGroup>
```

**User experience**: `dotnet add package Zvec` -- that is all. NuGet resolves the native binary.

---

## C-API binding details

### Key design principles from Zvec's C-API (verified against c_api.h)

1. **Opaque pointers**: all objects are `typedef struct zvec_xxx_t`. C# sees `IntPtr` (wrapped in `SafeHandle`).
2. **Create/destroy pairs**: every object has `zvec_xxx_create()` / `zvec_xxx_destroy()`. Map to `SafeHandle` + `IDisposable`.
3. **Error codes**: most functions return `zvec_error_code_t` (uint32 enum, 0-10). Map to a C# enum and throw `ZvecException` on non-zero.
4. **Error messages**: `zvec_get_last_error(char **error_msg)` returns error code AND allocates a message string via out param. Caller frees with `free()` (standard C free, NOT `zvec_free`). Thread safety of this call is undocumented but allocation pattern suggests it's safe to call from the thread that got the error.
5. **Type constants**: data types, index types, metric types are `uint32_t` `#define` constants (not C enums). Map to C# enums with explicit uint values.
6. **Ownership rules** (verified from c_api.h comments):
   - `zvec_collection_schema_add_field`: **clones** the field, caller retains ownership and must destroy field
   - `zvec_collection_create_index`: **copies** index_params, caller retains ownership
   - `zvec_collection_add_column`: **deep-copies** field_schema, caller retains ownership
   - `zvec_vector_query_set_hnsw_params`: **takes ownership** -- do NOT destroy params after this call
   - `zvec_vector_query_set_ivf_params`: **takes ownership**
   - `zvec_vector_query_set_flat_params`: **takes ownership**
   - `zvec_vector_query_set_fts_params`: **takes ownership**
   - `zvec_config_data_set_log_config`: **takes ownership** of log_config
   - `zvec_collection_insert/update/upsert`: takes `const zvec_doc_t**` -- **borrows**, caller retains ownership and must destroy docs
7. **close vs destroy for collections**: both exist as separate functions. `zvec_collection_close()` flushes and releases the collection. `zvec_collection_destroy()` frees the handle memory. SafeHandle should call both.

### Minimum viable P/Invoke surface (verified against c_api.h)

All function names below are exact matches from the official c_api.h header (3336 lines).
Calling convention is `__cdecl` on Windows (`ZVEC_CALL`).

**Group 1: Init, version, memory (~6 functions)**
```c
zvec_error_code_t zvec_initialize(const zvec_config_data_t *config);  // NULL = defaults
zvec_error_code_t zvec_shutdown(void);
bool              zvec_is_initialized(void);
const char*       zvec_get_version(void);             // internal string, do NOT free
void              zvec_free(void *ptr);
void*             zvec_malloc(size_t size);
```

**Group 2: Error handling (~3 functions)**
```c
zvec_error_code_t zvec_get_last_error(char **error_msg);         // caller frees with free()
zvec_error_code_t zvec_get_last_error_details(zvec_error_details_t *details);
void              zvec_clear_error(void);
```

**Group 3: Field schema (~4 functions for MVP)**
```c
// Single constructor for both vector and scalar fields.
// For scalar fields: dimension = 0. For vector fields: dimension > 0.
zvec_field_schema_t* zvec_field_schema_create(const char *name, zvec_data_type_t data_type,
                                               bool nullable, uint32_t dimension);
void                 zvec_field_schema_destroy(zvec_field_schema_t *schema);
zvec_error_code_t    zvec_field_schema_set_index_params(zvec_field_schema_t *schema,
                                                         const zvec_index_params_t *params);
zvec_error_code_t    zvec_field_schema_validate(const zvec_field_schema_t *schema,
                                                 zvec_string_t **error_msg);
```

**Group 4: Index params (~4 functions for MVP)**
```c
zvec_index_params_t* zvec_index_params_create(zvec_index_type_t index_type);
void                 zvec_index_params_destroy(zvec_index_params_t *params);
zvec_error_code_t    zvec_index_params_set_metric_type(zvec_index_params_t *params,
                                                        zvec_metric_type_t metric_type);
zvec_error_code_t    zvec_index_params_set_hnsw_params(zvec_index_params_t *params,
                                                        int m, int ef_construction);
```

**Group 5: Collection schema (~4 functions for MVP)**
```c
zvec_collection_schema_t* zvec_collection_schema_create(const char *name);
void                      zvec_collection_schema_destroy(zvec_collection_schema_t *schema);
zvec_error_code_t         zvec_collection_schema_add_field(zvec_collection_schema_t *schema,
                                                            const zvec_field_schema_t *field);
                          // ^ field is CLONED, caller retains ownership
zvec_error_code_t         zvec_collection_schema_validate(const zvec_collection_schema_t *schema,
                                                           zvec_string_t **error_msg);
```

**Group 6: Collection options (~4 functions for MVP)**
```c
zvec_collection_options_t* zvec_collection_options_create(void);
void                       zvec_collection_options_destroy(zvec_collection_options_t *options);
zvec_error_code_t          zvec_collection_options_set_enable_mmap(zvec_collection_options_t *opts, bool enable);
zvec_error_code_t          zvec_collection_options_set_read_only(zvec_collection_options_t *opts, bool read_only);
```

**Group 7: Collection lifecycle (~7 functions)**
```c
zvec_error_code_t zvec_collection_create_and_open(const char *path,
                    const zvec_collection_schema_t *schema,
                    const zvec_collection_options_t *options,  // NULL = defaults
                    zvec_collection_t **collection);           // out param
zvec_error_code_t zvec_collection_open(const char *path,
                    const zvec_collection_options_t *options,
                    zvec_collection_t **collection);
zvec_error_code_t zvec_collection_close(zvec_collection_t *collection);
zvec_error_code_t zvec_collection_destroy(zvec_collection_t *collection);
zvec_error_code_t zvec_collection_flush(zvec_collection_t *collection);
zvec_error_code_t zvec_collection_create_index(zvec_collection_t *collection,
                    const char *field_name,
                    const zvec_index_params_t *index_params);  // copied, caller retains
zvec_error_code_t zvec_collection_optimize(zvec_collection_t *collection);
```

**Group 8: Document (~5 functions for MVP)**
```c
zvec_doc_t*       zvec_doc_create(void);
void              zvec_doc_destroy(zvec_doc_t *doc);
void              zvec_doc_set_pk(zvec_doc_t *doc, const char *pk);

// Generic field setter -- used for ALL field types including vectors.
// For scalar: value points to the value, value_size = sizeof(type).
// For vector fp32: value points to float[], value_size = dimension * sizeof(float).
zvec_error_code_t zvec_doc_add_field_by_value(zvec_doc_t *doc, const char *field_name,
                    zvec_data_type_t data_type, const void *value, size_t value_size);

// Batch free (for result arrays)
void              zvec_docs_free(zvec_doc_t **documents, size_t count);
```

**Group 9: DML (~4 functions for MVP)**
```c
// All take const docs -- borrows, caller must destroy docs after call
zvec_error_code_t zvec_collection_insert(zvec_collection_t *collection,
                    const zvec_doc_t **docs, size_t doc_count,
                    size_t *success_count, size_t *error_count);
zvec_error_code_t zvec_collection_update(zvec_collection_t *collection,
                    const zvec_doc_t **docs, size_t doc_count,
                    size_t *success_count, size_t *error_count);
zvec_error_code_t zvec_collection_upsert(zvec_collection_t *collection,
                    const zvec_doc_t **docs, size_t doc_count,
                    size_t *success_count, size_t *error_count);
zvec_error_code_t zvec_collection_delete(zvec_collection_t *collection,
                    const char *const *pks, size_t pk_count,
                    size_t *success_count, size_t *error_count);
```

**Group 10: Vector query (~6 functions for MVP)**
```c
zvec_vector_query_t* zvec_vector_query_create(void);  // no args! field set separately
void                 zvec_vector_query_destroy(zvec_vector_query_t *query);
zvec_error_code_t    zvec_vector_query_set_topk(zvec_vector_query_t *query, int topk);
zvec_error_code_t    zvec_vector_query_set_field_name(zvec_vector_query_t *query, const char *name);
// Vector data is raw bytes. For fp32: pass float*, size = dimension * sizeof(float)
zvec_error_code_t    zvec_vector_query_set_query_vector(zvec_vector_query_t *query,
                       const void *data, size_t size);
zvec_error_code_t    zvec_vector_query_set_filter(zvec_vector_query_t *query, const char *filter);
```

**Group 11: Query execution + result reading (~5 functions)**
```c
// Returns array of doc pointers via out params. Free with zvec_docs_free.
zvec_error_code_t zvec_collection_query(const zvec_collection_t *collection,
                    const zvec_vector_query_t *query,
                    zvec_doc_t ***results, size_t *result_count);

// Read result doc fields
const char*       zvec_doc_get_pk_pointer(const zvec_doc_t *doc);  // internal, do NOT free
const char*       zvec_doc_get_pk_copy(const zvec_doc_t *doc);     // caller frees with free()
float             zvec_doc_get_score(const zvec_doc_t *doc);
zvec_error_code_t zvec_doc_get_field_value_basic(const zvec_doc_t *doc,
                    const char *field_name, zvec_data_type_t field_type,
                    void *value_buffer, size_t buffer_size);
```

Total MVP surface: ~52 functions across 11 groups.

---

## SafeHandle implementation

Every opaque pointer type gets its own SafeHandle subclass:

```csharp
// Collection handle calls both close + destroy
internal class CollectionHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private CollectionHandle() : base(true) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.zvec_collection_close(handle);
        NativeMethods.zvec_collection_destroy(handle);
        return true;
    }
}

// Most other handles just call destroy
internal class DocumentHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private DocumentHandle() : base(true) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.zvec_doc_destroy(handle);
        return true;
    }
}
```

Critical rules:
- `SafeHandle` prevents double-free and ensures cleanup even on exceptions/finalizer
- When a C function takes ownership of a handle (e.g. `zvec_vector_query_set_hnsw_params`), call `handle.SetHandleAsInvalid()` after the call so the finalizer does not double-free
- Most schema/field/index operations CLONE rather than take ownership (caller still destroys)
- All handles should be `internal` -- users never see `IntPtr` or handles

---

## Public API design

The public API should feel similar to the Python API. Target usage:

```csharp
// Target user experience
using var collection = ZvecCollection.CreateAndOpen("./my_vectors", schema =>
{
    schema.Name = "documents";
    schema.AddVector("embedding", dimensions: 1024, metric: MetricType.Cosine);
    schema.AddScalar("title", DataType.String);
});

// Insert -- doc must be disposed by caller (insert borrows, does not take ownership)
using (var doc = new ZvecDocument("doc_1"))
{
    doc.SetVector("embedding", embeddingFloat32Array);
    doc.SetString("title", "My document");
    collection.Insert(doc);
}

// Create index (required before first query)
collection.CreateIndex("embedding");

// Query
var results = collection.Query(
    VectorQuery.For("embedding", queryVector, topK: 10)
);

foreach (var result in results)
{
    Console.WriteLine($"{result.Id}: {result.Score}");
}
```

### Class: ZvecCollection

- Implements `IDisposable`
- Static factory methods: `CreateAndOpen(path, schemaBuilder)`, `Open(path)`
- Instance methods: `Insert(doc)`, `Insert(docs)`, `Update(doc)`, `Upsert(doc)`, `Delete(id)`, `CreateIndex(fieldName, indexParams)`, `Query(vectorQuery)`, `Flush()`, `Close()`
- Query returns `IReadOnlyList<SearchResult>`
- Thread safety: reads are concurrent-safe per Zvec docs. Document this.
- Internal: holds a `CollectionHandle`

### Class: ZvecDocument

- Constructor takes string PK: `new ZvecDocument("my_id")`
- Typed convenience setters built in managed code on top of `zvec_doc_add_field_by_value`:
  - `SetVector(fieldName, float[])` -- pins array, passes as void* with size = dim * 4
  - `SetString(fieldName, value)` -- marshals to UTF-8, passes pointer + byte length
  - `SetInt32(fieldName, value)` -- passes &value with size = 4
  - `SetInt64(fieldName, value)`, `SetFloat(fieldName, value)`, `SetDouble(fieldName, value)`, `SetBool(fieldName, value)`
- Implements `IDisposable` (wraps a `DocumentHandle`)
- Insert borrows the handle (const), so doc is still valid and must be disposed after insert

### Class: ZvecSchema (internal, used via builder Action)

- `AddVector(name, dimensions, metric, indexType)` -- creates field schema with data_type=VECTOR_FP32 + dimension, creates index_params, sets metric, attaches to field, adds to collection schema, destroys field + params
- `AddScalar(name, dataType)` -- creates field schema with dimension=0, adds to collection schema, destroys field
- Uses single `zvec_field_schema_create(name, type, nullable, dimension)` for both
- All handles destroyed in finally blocks -- add_field clones, so safe to destroy immediately after

### Class: VectorQuery

- Static factory: `VectorQuery.For(fieldName, vector, topK)` -- internally: create(), set_field_name(), set_query_vector(void*, byte_size), set_topk()
- `set_query_vector` takes raw bytes -- for fp32, pin float[] and pass with size = length * sizeof(float)
- Optional: `WithFilter(expr)` for filter expressions
- Implements `IDisposable`

### Class: SearchResult

- Simple POCO: `string Id`, `float Score`
- Optionally: `Dictionary<string, object> Fields` for retrieved scalar fields (add later)

### Class: ZvecException

- Extends `Exception`
- Has `ErrorCode` property (the zvec error code enum)
- Helper predicates: `IsNotFound`, `IsAlreadyExists`, `IsInvalidArgument`

### Enums

```csharp
public enum DataType : uint
{
    Undefined    = 0,
    Binary       = 1,
    String       = 2,
    Bool         = 3,
    Int32        = 4,
    Int64        = 5,
    UInt32       = 6,
    UInt64       = 7,
    Float        = 8,
    Double       = 9,
    VectorFp16   = 22,
    VectorFp32   = 23,
    VectorFp64   = 24,
    VectorInt8   = 26,
    // Sparse/array types out of scope for v1
}

public enum MetricType : uint
{
    Undefined    = 0,
    L2           = 1,
    InnerProduct = 2,
    Cosine       = 3,
}

public enum IndexType : uint
{
    Undefined = 0,
    Hnsw      = 1,
    Ivf       = 2,
    Flat      = 3,
}

public enum ZvecErrorCode : uint
{
    Ok                 = 0,
    NotFound           = 1,
    AlreadyExists      = 2,
    InvalidArgument    = 3,
    PermissionDenied   = 4,
    FailedPrecondition = 5,
    ResourceExhausted  = 6,
    Unavailable        = 7,
    InternalError      = 8,
    NotSupported       = 9,
    Unknown            = 10,
}
```

All values verified from c_api.h `#define` constants and `zvec_error_code_t` enum.

---

## Error handling pattern

Every P/Invoke call should go through a helper:

```csharp
internal static class ZvecError
{
    public static void ThrowIfFailed(uint errorCode)
    {
        if (errorCode == 0) return;

        string message = "Unknown zvec error";

        // zvec_get_last_error writes an allocated string to the out param.
        // Caller frees with standard free() -- Marshal.FreeHGlobal or NativeMethods.zvec_free.
        IntPtr msgPtr = IntPtr.Zero;
        NativeMethods.zvec_get_last_error(out msgPtr);
        if (msgPtr != IntPtr.Zero)
        {
            message = Marshal.PtrToStringUTF8(msgPtr);
            Marshal.FreeHGlobal(msgPtr);  // allocated with standard malloc per c_api.h
        }

        throw new ZvecException((ZvecErrorCode)errorCode, message);
    }
}

// Usage in the public API:
public void Flush()
{
    ZvecError.ThrowIfFailed(NativeMethods.zvec_collection_flush(_handle));
}
```

---

## Native build scripts

### native/build-native.ps1 (Windows)

```
# Clone zvec at pinned tag
# Run cmake with MSVC, Release, shared libs ON
# Copy output DLL to runtimes/win-x64/native/
```

### native/build-native.sh (Linux/macOS)

```
# Clone zvec at pinned tag
# Run cmake with gcc/clang, Release, shared libs ON
# Copy output .so/.dylib to runtimes/{rid}/native/
```

Pin to a specific zvec release tag (e.g., `v0.3.0`) to ensure reproducible builds. Put the tag in a variable at the top of each script.

---

## CI / GitHub Actions

### build-native.yml

Matrix strategy across 3 runners:
- `ubuntu-latest` -> produces `libzvec_c_api.so` -> artifact `native-linux-x64`
- `macos-14` (ARM) -> produces `libzvec_c_api.dylib` -> artifact `native-osx-arm64`
- `windows-latest` -> produces `zvec_c_api.dll` -> artifact `native-win-x64`

Each runner:
1. Install cmake, build tools
2. Run the appropriate native build script
3. Upload artifact

### ci.yml

Triggered on push/PR:
1. Download all 3 native artifacts from `build-native.yml`
2. Place in `runtimes/` folders
3. `dotnet build`
4. `dotnet test` (on the current runner's platform -- native lib must match)
5. `dotnet pack` (only on release tags)
6. Push to NuGet (only on release tags, with API key secret)

---

## Testing strategy

Tests require the native library for the current platform to be present. In CI, downloaded from artifacts. Locally, developer runs the native build script first.

### SchemaTests.cs
- Create schema with vector field -> no error
- Create schema with scalar fields -> no error
- Create schema with invalid dimension (0, negative) -> throws ZvecException

### CrudTests.cs
- Insert single document -> verify no error
- Insert multiple documents -> verify count
- Upsert existing document -> verify no error
- Delete by ID -> verify no error
- Insert duplicate PK -> verify behavior (error or upsert depending on zvec semantics)

### SearchTests.cs
- Insert 100 vectors, build index, query -> returns results
- Query with topK=5 -> returns exactly 5 results
- Results are sorted by score (most similar first)
- Query with a vector identical to an inserted one -> score is 1.0 (or 0.0 for L2)

### ConcurrencyTests.cs
- Multiple concurrent reads -> no crash
- Read during write -> no crash (zvec claims thread-safe reads)

### ErrorHandlingTests.cs
- Open non-existent path -> throws with appropriate error code
- Query without building index first -> verify behavior
- Close collection then call method -> throws ObjectDisposedException

---

## Things to be careful about

1. **String marshaling**: Zvec C-API uses UTF-8 strings. Use `[MarshalAs(UnmanagedType.LPUTF8Str)]` on .NET 8+ or manually marshal with `Encoding.UTF8`.

2. **Generic field setter**: `zvec_doc_add_field_by_value` takes `void*` + `size_t`. For vectors, pin the float array and pass `dimension * sizeof(float)` as size. For scalars, pass `&value` with `sizeof(type)`. For strings, marshal to UTF-8 bytes and pass the byte pointer + byte length. Build typed C# wrappers (SetString, SetInt32, SetVector, etc.) to hide this from users.

3. **Ownership rules**: Most schema/field/index operations CLONE arguments (caller retains and must destroy). BUT `zvec_vector_query_set_hnsw_params` and similar query param setters TAKE OWNERSHIP. After those calls, `SetHandleAsInvalid()` on the params handle.

4. **Library loading**: use `NativeLibrary.SetDllImportResolver` to customize loading if needed. The DllImport name should match across platforms -- .NET handles the extension mapping (`zvec_c_api` -> `zvec_c_api.dll` on Windows, `libzvec_c_api.so` on Linux, `libzvec_c_api.dylib` on macOS).

5. **Use LibraryImport**: target `net8.0` minimum. Use `[LibraryImport]` (source-generated P/Invoke) instead of `[DllImport]` for better AOT support and no runtime marshaling stubs.

6. **Platform detection**: if a user tries to use the library on a platform without a native binary, give a clear error message ("Zvec native library not found for platform {rid}.").

7. **Pinned zvec version**: the managed package version should track the zvec native version (e.g., Zvec 0.3.0 wraps zvec v0.3.0). Document this in README.

8. **Query vector is raw bytes**: `zvec_vector_query_set_query_vector(query, void* data, size_t size)` takes untyped bytes. For fp32 queries, pin the float[] and pass `length * 4` as size.

9. **Collection close + destroy**: both must be called. close flushes and releases resources, destroy frees the handle. SafeHandle.ReleaseHandle should call both in order.

10. **zvec_initialize must be called once**: before any collection operations. Pass NULL for default config. Guard with `zvec_is_initialized()`. Use a static initializer or `Lazy<>` pattern.

11. **Never use unicode or emoji in code**: use plain ASCII throughout.

---

## Execution order

1. ~~Read `c_api.h` thoroughly.~~ DONE. c_api.h has been downloaded and fully analyzed. MVP surface is ~52 functions across 11 groups. All names, signatures, and ownership rules documented above.
2. Scaffold repo structure, sln, csproj files, Directory.Build.props.
3. Write `ErrorCodes.cs` and enum types (`DataType`, `MetricType`, `IndexType`) with exact values from the header.
4. Write `NativeMethods.cs` -- all P/Invoke declarations, split by group into partial classes or separate files.
5. Write `SafeHandle` subclasses in `Handles/`.
6. Write the error handling helper (`ZvecError.ThrowIfFailed`).
7. Write `ZvecSchema` (builder + handle management).
8. Write `ZvecDocument` (builder + handle management).
9. Write `VectorQuery` (builder + handle management).
10. Write `ZvecCollection` (the main class tying it all together).
11. Write `ZvecException` and `SearchResult`.
12. Write the sample app (`samples/BasicSearch/Program.cs`).
13. Write tests.
14. Write native build scripts.
15. Write GitHub Actions workflows.
16. Write README with usage examples, build instructions, and contribution guide.

Steps 1-13 are the core deliverable. Steps 14-16 are infrastructure.

---

## Out of scope for v1 (add later)

- Sparse vector support
- Reranking (weighted fusion, RRF)
- Filter expressions on queries
- Index parameter tuning (HNSW ef_construction, M, etc.)
- Quantization configuration (RabitQ, SQ8)
- MCP / AI agent integration
- Batch query API
- Schema evolution (alter fields)
- linux-arm64 and osx-x64 native builds
- Source Link / deterministic builds
- XML doc comments on all public API
