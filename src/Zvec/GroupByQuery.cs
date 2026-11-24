using Zvec.Native;

namespace Zvec;

/// <summary>
/// Vector search grouped by a scalar field: the best matches per distinct value
/// of that field (e.g. top chunks per document). Requires zvec v0.8.1+.
/// </summary>
public class GroupByQuery : IDisposable
{
    private nint _handle;
    private bool _disposed;

    private GroupByQuery(nint handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Create a grouped vector query.
    /// </summary>
    /// <param name="fieldName">Vector field to search.</param>
    /// <param name="vector">Query vector (float32).</param>
    /// <param name="groupByField">Scalar field whose values define the groups.</param>
    /// <param name="groupCount">Maximum number of groups to return.</param>
    /// <param name="topKPerGroup">Maximum number of results in each group.</param>
    public static unsafe GroupByQuery For(string fieldName, float[] vector, string groupByField,
        int groupCount, int topKPerGroup)
    {
        nint handle = NativeMethods.zvec_group_by_vector_query_create();
        if (handle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create group-by query");

        var query = new GroupByQuery(handle);
        try
        {
            ZvecError.ThrowIfFailed(NativeMethods.zvec_group_by_vector_query_set_field_name(handle, fieldName));
            ZvecError.ThrowIfFailed(NativeMethods.zvec_group_by_vector_query_set_group_by_field_name(handle, groupByField));
            ZvecError.ThrowIfFailed(NativeMethods.zvec_group_by_vector_query_set_group_count(handle, (uint)groupCount));
            ZvecError.ThrowIfFailed(NativeMethods.zvec_group_by_vector_query_set_topk_per_group(handle, (uint)topKPerGroup));

            fixed (float* ptr = vector)
            {
                ZvecError.ThrowIfFailed(
                    NativeMethods.zvec_group_by_vector_query_set_query_vector(
                        handle, ptr, (nuint)(vector.Length * sizeof(float))));
            }
        }
        catch
        {
            query.Dispose();
            throw;
        }

        return query;
    }

    /// <summary>
    /// Add a filter expression (e.g. "year > 2020").
    /// </summary>
    public GroupByQuery WithFilter(string filterExpression)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(NativeMethods.zvec_group_by_vector_query_set_filter(_handle, filterExpression));
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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != 0)
        {
            NativeMethods.zvec_group_by_vector_query_destroy(_handle);
            _handle = 0;
        }

        GC.SuppressFinalize(this);
    }

    ~GroupByQuery()
    {
        if (_handle != 0)
        {
            NativeMethods.zvec_group_by_vector_query_destroy(_handle);
            _handle = 0;
        }
    }
}

/// <summary>
/// One group of a grouped search: the grouping value and its matches, best first.
/// </summary>
public class SearchGroup
{
    /// <summary>String form of the grouping field's value.</summary>
    public string Value { get; }

    public IReadOnlyList<SearchResult> Results { get; }

    internal SearchGroup(string value, IReadOnlyList<SearchResult> results)
    {
        Value = value;
        Results = results;
    }
}
