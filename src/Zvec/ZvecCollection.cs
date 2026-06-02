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
    /// Create an index on a vector field.
    /// </summary>
    public void CreateIndex(string fieldName, IndexType indexType = IndexType.Hnsw,
        MetricType metric = MetricType.Cosine)
    {
        ThrowIfDisposed();

        nint paramsHandle = NativeMethods.zvec_index_params_create((uint)indexType);
        if (paramsHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create index params");

        try
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_index_params_set_metric_type(paramsHandle, (uint)metric));

            // create_index copies params, caller retains ownership
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
