using System.Runtime.InteropServices;
using Zvec.Native;

namespace Zvec;

/// <summary>
/// Represents an open zvec collection. Supports insert, update, upsert, delete,
/// vector search, index management, and schema evolution. Implements IDisposable.
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
    /// Create a new collection on disk and open it.
    /// </summary>
    /// <param name="path">Directory path for the collection data.</param>
    /// <param name="configureSchema">Action to configure the collection schema (vector fields, scalar fields).</param>
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
    /// Open an existing collection from disk.
    /// </summary>
    /// <param name="path">Directory path of the collection.</param>
    public static ZvecCollection Open(string path) => Open(path, readOnly: false);

    /// <summary>
    /// Open an existing collection from disk, optionally read-only. Several processes
    /// can hold the same collection read-only. A collection with pending WAL recovery
    /// has to be opened and closed writable once before it opens read-only (zvec v0.8.1).
    /// </summary>
    /// <param name="path">Directory path of the collection.</param>
    /// <param name="readOnly">Open without write access.</param>
    public static ZvecCollection Open(string path, bool readOnly)
    {
        ZvecRuntime.Initialize();

        if (!readOnly)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_open(path, 0, out nint handle));
            return new ZvecCollection(handle);
        }

        nint options = NativeMethods.zvec_collection_options_create();
        if (options == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create collection options");

        try
        {
            ZvecError.ThrowIfFailed(NativeMethods.zvec_collection_options_set_read_only(options, true));
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_open(path, options, out nint handle));
            return new ZvecCollection(handle);
        }
        finally
        {
            NativeMethods.zvec_collection_options_destroy(options);
        }
    }

    /// <summary>
    /// Get the document count and per-index build completeness.
    /// </summary>
    public CollectionStats GetStats()
    {
        ThrowIfDisposed();

        ZvecError.ThrowIfFailed(NativeMethods.zvec_collection_get_stats(_handle, out nint stats));
        try
        {
            ulong docCount = NativeMethods.zvec_collection_stats_get_doc_count(stats);
            nuint indexCount = NativeMethods.zvec_collection_stats_get_index_count(stats);

            var indexes = new List<IndexStats>((int)indexCount);
            for (nuint i = 0; i < indexCount; i++)
            {
                string name = Marshal.PtrToStringUTF8(
                    NativeMethods.zvec_collection_stats_get_index_name(stats, i)) ?? "";
                indexes.Add(new IndexStats(name,
                    NativeMethods.zvec_collection_stats_get_index_completeness(stats, i)));
            }

            return new CollectionStats(docCount, indexes);
        }
        finally
        {
            NativeMethods.zvec_collection_stats_destroy(stats);
        }
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
    /// <param name="fieldName">Vector field to index.</param>
    /// <param name="metric">Distance metric to use.</param>
    /// <param name="m">Graph connectivity (default: 16). Higher = better recall, more memory.</param>
    /// <param name="efConstruction">Build-time exploration factor (default: 200). Higher = better recall, slower build.</param>
    /// <param name="quantization">Quantization type for reduced memory usage.</param>
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
    /// Create a full-text search index on an existing string field.
    /// Null tokenizer/filters/extraParams keep zvec's defaults. Rows already in the
    /// collection get indexed too. Needs zvec v0.8+ (v0.5.1 returned NotSupported and
    /// only took FTS indexes at schema time via ZvecSchema.AddText()).
    /// </summary>
    public void CreateFtsIndex(string fieldName, string? tokenizerName = null,
        IReadOnlyList<string>? filters = null, string? extraParams = null)
    {
        ThrowIfDisposed();

        nint paramsHandle = NativeMethods.zvec_index_params_create((uint)IndexType.Fts);
        if (paramsHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create FTS index params");

        try
        {
            // no metric type: FTS is not a distance-metric index
            FtsInterop.ApplyFtsParams(paramsHandle, tokenizerName, filters, extraParams);

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
    /// Execute a multi-field query with reranking (hybrid search).
    /// </summary>
    public unsafe IReadOnlyList<SearchResult> Query(MultiQuery query)
    {
        ThrowIfDisposed();

        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_collection_multi_query(_handle, query.Handle, out nint* resultDocs, out nuint resultCount));

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
            NativeMethods.zvec_docs_free((nint)resultDocs, resultCount);
        }

        return results;
    }

    /// <summary>
    /// Execute a grouped vector search: best matches per distinct value of the
    /// group-by field. Groups come back best first. Requires zvec v0.8.1+.
    /// </summary>
    public unsafe IReadOnlyList<SearchGroup> Query(GroupByQuery query)
    {
        ThrowIfDisposed();

        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_collection_group_by_query(_handle, query.Handle,
                out GroupResult* groups, out nuint groupCount));

        var results = new List<SearchGroup>((int)groupCount);

        try
        {
            for (nuint g = 0; g < groupCount; g++)
            {
                string value = Marshal.PtrToStringUTF8(groups[g].GroupByValue) ?? "";
                var docs = new List<SearchResult>((int)groups[g].DocCount);

                for (nuint i = 0; i < groups[g].DocCount; i++)
                {
                    nint docPtr = groups[g].Docs[i];
                    string pk = Marshal.PtrToStringUTF8(NativeMethods.zvec_doc_get_pk_pointer(docPtr)) ?? "";
                    docs.Add(new SearchResult(pk, NativeMethods.zvec_doc_get_score(docPtr)));
                }

                results.Add(new SearchGroup(value, docs));
            }
        }
        finally
        {
            // frees the group values and every doc in one go (NULL is allowed)
            NativeMethods.zvec_group_results_free(groups, groupCount);
        }

        return results;
    }

    /// <summary>
    /// Execute a vector similarity or full-text search.
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
    /// Delete every document matching a filter expression (e.g. "year &lt; 2020").
    /// </summary>
    public void DeleteByFilter(string filterExpression)
    {
        ThrowIfDisposed();
        ZvecError.ThrowIfFailed(NativeMethods.zvec_collection_delete_by_filter(_handle, filterExpression));
    }

    /// <summary>
    /// Walk every document in the collection (a snapshot taken when enumeration starts;
    /// later writes are not visible). Each yielded doc is only readable until the next
    /// one is requested. Always enumerate with foreach or dispose the enumerator: zvec
    /// refuses schema changes and Optimize while a scan is open, and closing the
    /// collection waits for it. On a writable collection each scan seals the current
    /// writing segment.
    /// </summary>
    /// <param name="outputFields">Fields to return; null = all fields.</param>
    /// <param name="includeVector">Include vector data (off by default - it's big).</param>
    public IEnumerable<FetchedDoc> Scan(IReadOnlyList<string>? outputFields = null, bool includeVector = false)
    {
        nint iterator = CreateIterator(outputFields, includeVector);
        FetchedDoc? current = null;

        try
        {
            while (true)
            {
                ZvecError.ThrowIfFailed(NativeMethods.zvec_doc_iterator_next(iterator, out nint doc));

                current?.Release();
                current = null;

                if (doc == 0)
                    yield break; // end of collection

                current = new FetchedDoc(null, doc);
                yield return current;
            }
        }
        finally
        {
            current?.Release();
            NativeMethods.zvec_doc_iterator_close(iterator);
        }
    }

    // Split out of Scan because iterator methods can't contain unsafe code.
    private unsafe nint CreateIterator(IReadOnlyList<string>? outputFields, bool includeVector)
    {
        ThrowIfDisposed();

        nint options = NativeMethods.zvec_iterator_options_create();
        if (options == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create iterator options");

        var fieldPtrs = outputFields is { Count: > 0 } ? new nint[outputFields.Count] : null;

        try
        {
            // native default is include_vector = true, ours is false
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_iterator_options_set_include_vector(options, includeVector));

            if (fieldPtrs != null)
            {
                for (int i = 0; i < fieldPtrs.Length; i++)
                    fieldPtrs[i] = Marshal.StringToCoTaskMemUTF8(outputFields![i]);

                fixed (nint* fields = fieldPtrs)
                {
                    ZvecError.ThrowIfFailed(
                        NativeMethods.zvec_iterator_options_set_output_fields(
                            options, fields, (nuint)fieldPtrs.Length));
                }
            }

            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_create_iterator(_handle, options, out nint iterator));
            return iterator;
        }
        finally
        {
            if (fieldPtrs != null)
                foreach (var p in fieldPtrs) if (p != 0) Marshal.FreeCoTaskMem(p);
            NativeMethods.zvec_iterator_options_destroy(options);
        }
    }

    /// <summary>
    /// Fetch documents by primary key. Missing keys are silently absent from the
    /// result. Dispose the FetchResult when done reading - field pointers live
    /// inside the native doc array.
    /// </summary>
    /// <param name="primaryKeys">Keys to fetch.</param>
    /// <param name="outputFields">Fields to return; null = all fields.</param>
    /// <param name="includeVector">Include vector data (off by default - it's big).</param>
    public unsafe FetchResult Fetch(IReadOnlyList<string> primaryKeys,
        IReadOnlyList<string>? outputFields = null, bool includeVector = false)
    {
        ThrowIfDisposed();

        var pkPtrs = new nint[primaryKeys.Count];
        var fieldPtrs = outputFields is { Count: > 0 } ? new nint[outputFields.Count] : null;

        try
        {
            for (int i = 0; i < primaryKeys.Count; i++)
                pkPtrs[i] = Marshal.StringToCoTaskMemUTF8(primaryKeys[i]);
            if (fieldPtrs != null)
                for (int i = 0; i < outputFields!.Count; i++)
                    fieldPtrs[i] = Marshal.StringToCoTaskMemUTF8(outputFields[i]);

            fixed (nint* pks = pkPtrs)
            fixed (nint* fields = fieldPtrs)
            {
                ZvecError.ThrowIfFailed(NativeMethods.zvec_collection_fetch(
                    _handle, pks, (nuint)primaryKeys.Count,
                    fieldPtrs != null ? fields : null,
                    (nuint)(outputFields?.Count ?? 0),
                    includeVector, out nint* docs, out nuint foundCount));

                return new FetchResult(docs, foundCount);
            }
        }
        finally
        {
            foreach (var p in pkPtrs) if (p != 0) Marshal.FreeCoTaskMem(p);
            if (fieldPtrs != null)
                foreach (var p in fieldPtrs) if (p != 0) Marshal.FreeCoTaskMem(p);
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
