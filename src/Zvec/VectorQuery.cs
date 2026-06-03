using Zvec.Native;

namespace Zvec;

/// <summary>
/// Builder for vector similarity queries.
/// </summary>
public class VectorQuery : IDisposable
{
    private nint _handle;
    private bool _disposed;

    private VectorQuery(nint handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Create a vector query for the given field and vector, returning topK results.
    /// </summary>
    public static unsafe VectorQuery For(string fieldName, float[] vector, int topK)
    {
        nint handle = NativeMethods.zvec_vector_query_create();
        if (handle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create vector query");

        ZvecError.ThrowIfFailed(NativeMethods.zvec_vector_query_set_field_name(handle, fieldName));
        ZvecError.ThrowIfFailed(NativeMethods.zvec_vector_query_set_topk(handle, topK));

        fixed (float* ptr = vector)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_vector_query_set_query_vector(handle, ptr, (nuint)(vector.Length * sizeof(float))));
        }

        return new VectorQuery(handle);
    }

    /// <summary>
    /// Add a filter expression to the query (e.g. "year > 2020 AND category = 'science'").
    /// </summary>
    public VectorQuery WithFilter(string filterExpression)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(NativeMethods.zvec_vector_query_set_filter(_handle, filterExpression));
        return this;
    }

    /// <summary>
    /// Add a full-text search expression. Requires an FTS index on a text field.
    /// </summary>
    /// <param name="queryString">FTS query expression (e.g. "quantum AND physics").</param>
    public VectorQuery WithFts(string queryString)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nint ftsHandle = NativeMethods.zvec_fts_create();
        if (ftsHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create FTS query");

        try
        {
            ZvecError.ThrowIfFailed(NativeMethods.zvec_fts_set_query_string(ftsHandle, queryString));
            ZvecError.ThrowIfFailed(NativeMethods.zvec_vector_query_set_fts(_handle, ftsHandle));
        }
        finally
        {
            NativeMethods.zvec_fts_destroy(ftsHandle);
        }

        return this;
    }

    /// <summary>
    /// Add a natural-language full-text match. Requires an FTS index on a text field.
    /// </summary>
    /// <param name="matchString">Natural language text to match against.</param>
    public VectorQuery WithFtsMatch(string matchString)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nint ftsHandle = NativeMethods.zvec_fts_create();
        if (ftsHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create FTS query");

        try
        {
            ZvecError.ThrowIfFailed(NativeMethods.zvec_fts_set_match_string(ftsHandle, matchString));
            ZvecError.ThrowIfFailed(NativeMethods.zvec_vector_query_set_fts(_handle, ftsHandle));
        }
        finally
        {
            NativeMethods.zvec_fts_destroy(ftsHandle);
        }

        return this;
    }

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != 0)
        {
            NativeMethods.zvec_vector_query_destroy(_handle);
            _handle = 0;
        }

        GC.SuppressFinalize(this);
    }

    ~VectorQuery()
    {
        if (_handle != 0)
        {
            NativeMethods.zvec_vector_query_destroy(_handle);
            _handle = 0;
        }
    }
}
