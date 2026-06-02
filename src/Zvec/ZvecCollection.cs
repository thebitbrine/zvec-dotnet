using System.Runtime.InteropServices;
using Zvec.Native;

namespace Zvec;

/// <summary>
/// Main entry point for zvec operations. Wraps a native collection handle.
/// </summary>
public class ZvecCollection : IDisposable
{
    private nint _handle;
    private bool _disposed;

    private ZvecCollection(nint handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Create a new collection and open it.
    /// </summary>
    public static ZvecCollection CreateAndOpen(string path, Action<ZvecSchema> configureSchema)
    {
        ZvecRuntime.Initialize();

        nint schemaHandle = NativeMethods.zvec_collection_schema_create("default");
        if (schemaHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create collection schema");

        try
        {
            var builder = new ZvecSchema(schemaHandle);
            configureSchema(builder);

            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_create_and_open(path, schemaHandle, 0, out nint collHandle));

            return new ZvecCollection(collHandle);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    /// <summary>
    /// Open an existing collection.
    /// </summary>
    public static ZvecCollection Open(string path)
    {
        ZvecRuntime.Initialize();

        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_collection_open(path, 0, out nint collHandle));

        return new ZvecCollection(collHandle);
    }

    /// <summary>
    /// Flush pending writes to disk.
    /// </summary>
    public void Flush()
    {
        ThrowIfDisposed();
        ZvecError.ThrowIfFailed(NativeMethods.zvec_collection_flush(_handle));
    }

    /// <summary>
    /// Create an index on a vector field with default HNSW parameters.
    /// </summary>
    public void CreateIndex(string fieldName, IndexType indexType = IndexType.Hnsw,
        MetricType metric = MetricType.Cosine)
    {
        CreateIndex(fieldName, indexType, metric, QuantizationType.Undefined);
    }

    /// <summary>
    /// Create an HNSW index with explicit tuning parameters.
    /// </summary>
    /// <param name="m">Graph connectivity (default: 16). Higher = better recall, more memory.</param>
    /// <param name="efConstruction">Build-time exploration factor (default: 200). Higher = better recall, slower build.</param>
    public void CreateHnswIndex(string fieldName, MetricType metric = MetricType.Cosine,
        int m = 16, int efConstruction = 200, QuantizationType quantization = QuantizationType.Undefined)
    {
        ThrowIfDisposed();

        nint paramsHandle = NativeMethods.zvec_index_params_create((uint)IndexType.Hnsw);
        if (paramsHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create index params");

        try
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_index_params_set_metric_type(paramsHandle, (uint)metric));
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_index_params_set_hnsw_params(paramsHandle, m, efConstruction));

            if (quantization != QuantizationType.Undefined)
                ZvecError.ThrowIfFailed(
                    NativeMethods.zvec_index_params_set_quantize_type(paramsHandle, (uint)quantization));

            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_create_index(_handle, fieldName, paramsHandle));
        }
        finally
        {
            NativeMethods.zvec_index_params_destroy(paramsHandle);
        }
    }

    /// <summary>
    /// Create an index with quantization and optional type-specific parameters.
    /// </summary>
    public void CreateIndex(string fieldName, IndexType indexType, MetricType metric,
        QuantizationType quantization)
    {
        ThrowIfDisposed();

        nint paramsHandle = NativeMethods.zvec_index_params_create((uint)indexType);
        if (paramsHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create index params");

        try
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_index_params_set_metric_type(paramsHandle, (uint)metric));

            if (quantization != QuantizationType.Undefined)
                ZvecError.ThrowIfFailed(
                    NativeMethods.zvec_index_params_set_quantize_type(paramsHandle, (uint)quantization));

            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_create_index(_handle, fieldName, paramsHandle));
        }
        finally
        {
            NativeMethods.zvec_index_params_destroy(paramsHandle);
        }
    }

    /// <summary>
    /// Optimize the collection (rebuild indexes, merge segments).
    /// </summary>
    public void Optimize()
    {
        ThrowIfDisposed();
        ZvecError.ThrowIfFailed(NativeMethods.zvec_collection_optimize(_handle));
    }

    /// <summary>
    /// Drop an index from a field.
    /// </summary>
    public void DropIndex(string fieldName)
    {
        ThrowIfDisposed();
        ZvecError.ThrowIfFailed(NativeMethods.zvec_collection_drop_index(_handle, fieldName));
    }

    // =========================================================================
    // Schema evolution (DDL)
    // =========================================================================

    /// <summary>
    /// Add a scalar column to the collection.
    /// </summary>
    public void AddColumn(string name, DataType dataType, bool nullable = true, string? defaultExpression = null)
    {
        ThrowIfDisposed();

        nint fieldHandle = NativeMethods.zvec_field_schema_create(name, (uint)dataType, nullable, 0);
        if (fieldHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create field schema");

        try
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_add_column(_handle, fieldHandle, defaultExpression));
        }
        finally
        {
            NativeMethods.zvec_field_schema_destroy(fieldHandle);
        }
    }

    /// <summary>
    /// Drop a column from the collection.
    /// </summary>
    public void DropColumn(string columnName)
    {
        ThrowIfDisposed();
        ZvecError.ThrowIfFailed(NativeMethods.zvec_collection_drop_column(_handle, columnName));
    }

    /// <summary>
    /// Rename a column.
    /// </summary>
    public void RenameColumn(string oldName, string newName)
    {
        ThrowIfDisposed();
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_collection_alter_column(_handle, oldName, newName, 0));
    }

    // =========================================================================
    // Query
    // =========================================================================

    /// <summary>
    /// Execute a vector similarity search.
    /// </summary>
    public unsafe IReadOnlyList<SearchResult> Query(VectorQuery query)
    {
        ThrowIfDisposed();

        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_collection_query(_handle, query.Handle, out nint* resultDocs, out nuint resultCount));

        var results = new List<SearchResult>((int)resultCount);

        try
        {
            for (nuint i = 0; i < resultCount; i++)
            {
                nint docPtr = resultDocs[i];
                nint pkPtr = NativeMethods.zvec_doc_get_pk_pointer(docPtr);
                string pk = Marshal.PtrToStringUTF8(pkPtr) ?? "";
                float score = NativeMethods.zvec_doc_get_score(docPtr);
                results.Add(new SearchResult(pk, score));
            }
        }
        finally
        {
            // Free the result array
            NativeMethods.zvec_docs_free((nint)resultDocs, resultCount);
        }

        return results;
    }

    // =========================================================================
    // DML operations
    // =========================================================================

    /// <summary>
    /// Insert a single document. Doc is borrowed, caller must dispose it.
    /// </summary>
    public unsafe void Insert(ZvecDocument doc)
    {
        ThrowIfDisposed();
        nint docHandle = doc.Handle;
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_collection_insert(_handle, &docHandle, 1, out _, out _));
    }

    /// <summary>
    /// Insert multiple documents. Docs are borrowed, caller must dispose them.
    /// </summary>
    public unsafe (nuint successCount, nuint errorCount) Insert(IReadOnlyList<ZvecDocument> docs)
    {
        ThrowIfDisposed();
        nint* handles = stackalloc nint[docs.Count];
        for (int i = 0; i < docs.Count; i++)
            handles[i] = docs[i].Handle;

        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_collection_insert(_handle, handles, (nuint)docs.Count,
                out nuint success, out nuint errors));

        return (success, errors);
    }

    /// <summary>
    /// Upsert a single document (insert or update by PK).
    /// </summary>
    public unsafe void Upsert(ZvecDocument doc)
    {
        ThrowIfDisposed();
        nint docHandle = doc.Handle;
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_collection_upsert(_handle, &docHandle, 1, out _, out _));
    }

    /// <summary>
    /// Update a single document by PK.
    /// </summary>
    public unsafe void Update(ZvecDocument doc)
    {
        ThrowIfDisposed();
        nint docHandle = doc.Handle;
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_collection_update(_handle, &docHandle, 1, out _, out _));
    }

    /// <summary>
    /// Delete a document by primary key.
    /// </summary>
    public unsafe void Delete(string primaryKey)
    {
        ThrowIfDisposed();
        nint pkPtr = Marshal.StringToCoTaskMemUTF8(primaryKey);
        try
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_delete(_handle, &pkPtr, 1, out _, out _));
        }
        finally
        {
            Marshal.FreeCoTaskMem(pkPtr);
        }
    }

    /// <summary>
    /// Close the collection and release native resources.
    /// </summary>
    public void Close()
    {
        Dispose();
    }

    internal nint Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != 0)
        {
            // close flushes and releases the collection handle
            NativeMethods.zvec_collection_close(_handle);
            _handle = 0;
        }

        GC.SuppressFinalize(this);
    }

    ~ZvecCollection()
    {
        if (_handle != 0)
        {
            NativeMethods.zvec_collection_close(_handle);
            _handle = 0;
        }
    }
}
