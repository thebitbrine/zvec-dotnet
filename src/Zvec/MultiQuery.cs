using System.Runtime.InteropServices;
using Zvec.Native;

namespace Zvec;

/// <summary>
/// Multi-vector query with optional reranking. Searches across multiple vector fields
/// and combines results using RRF or weighted fusion.
/// </summary>
public class MultiQuery : IDisposable
{
    private nint _handle;
    private readonly List<nint> _subQueryHandles = new();
    private bool _disposed;

    /// <summary>
    /// Create a multi-query returning the given number of results.
    /// </summary>
    public MultiQuery(int topK)
    {
        _handle = NativeMethods.zvec_multi_query_create();
        if (_handle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create multi-query");

        ZvecError.ThrowIfFailed(NativeMethods.zvec_multi_query_set_topk(_handle, topK));
    }

    /// <summary>
    /// Add a dense vector sub-query for a field.
    /// </summary>
    /// <param name="fieldName">Vector field to search.</param>
    /// <param name="vector">Query vector (float32).</param>
    /// <param name="numCandidates">Number of candidates to retrieve from this field before reranking.</param>
    public unsafe MultiQuery AddSubQuery(string fieldName, float[] vector, int numCandidates = 100)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nint subHandle = NativeMethods.zvec_sub_query_create();
        if (subHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create sub-query");

        ZvecError.ThrowIfFailed(NativeMethods.zvec_sub_query_set_field_name(subHandle, fieldName));
        ZvecError.ThrowIfFailed(NativeMethods.zvec_sub_query_set_num_candidates(subHandle, numCandidates));

        fixed (float* ptr = vector)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_sub_query_set_query_vector(subHandle, ptr, (nuint)(vector.Length * sizeof(float))));
        }

        ZvecError.ThrowIfFailed(NativeMethods.zvec_multi_query_add_sub_query(_handle, subHandle));
        _subQueryHandles.Add(subHandle);

        return this;
    }

    /// <summary>
    /// Add a sparse vector sub-query for a field.
    /// </summary>
    /// <param name="fieldName">Sparse vector field to search.</param>
    /// <param name="sparseVector">Sparse vector as index->value pairs.</param>
    /// <param name="numCandidates">Number of candidates to retrieve before reranking.</param>
    public unsafe MultiQuery AddSparseSubQuery(string fieldName, Dictionary<uint, float> sparseVector,
        int numCandidates = 100)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nint subHandle = NativeMethods.zvec_sub_query_create();
        if (subHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create sub-query");

        ZvecError.ThrowIfFailed(NativeMethods.zvec_sub_query_set_field_name(subHandle, fieldName));
        ZvecError.ThrowIfFailed(NativeMethods.zvec_sub_query_set_num_candidates(subHandle, numCandidates));

        var indices = new uint[sparseVector.Count];
        var values = new float[sparseVector.Count];
        int i = 0;
        foreach (var kv in sparseVector)
        {
            indices[i] = kv.Key;
            values[i] = kv.Value;
            i++;
        }

        fixed (uint* idxPtr = indices)
        fixed (float* valPtr = values)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_sub_query_set_sparse_vector(subHandle, idxPtr, valPtr, (nuint)sparseVector.Count));
        }

        ZvecError.ThrowIfFailed(NativeMethods.zvec_multi_query_add_sub_query(_handle, subHandle));
        _subQueryHandles.Add(subHandle);

        return this;
    }

    /// <summary>
    /// Use Reciprocal Rank Fusion to combine results from sub-queries.
    /// </summary>
    /// <param name="rankConstant">RRF constant k (default: 60). score = sum(1 / (k + rank_i))</param>
    public MultiQuery WithRrfReranker(int rankConstant = 60)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(NativeMethods.zvec_multi_query_set_rerank_rrf(_handle, rankConstant));
        return this;
    }

    /// <summary>
    /// Use weighted fusion to combine results. Weights are applied in sub-query order.
    /// </summary>
    /// <param name="weights">Per-sub-query weights in the same order as AddSubQuery calls.</param>
    public unsafe MultiQuery WithWeightedReranker(params double[] weights)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        fixed (double* wp = weights)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_multi_query_set_rerank_weighted(_handle, wp, (nuint)weights.Length));
        }
        return this;
    }

    /// <summary>
    /// Add a filter expression to the multi-query.
    /// </summary>
    public MultiQuery WithFilter(string filter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(NativeMethods.zvec_multi_query_set_filter(_handle, filter));
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

        foreach (var sh in _subQueryHandles)
            NativeMethods.zvec_sub_query_destroy(sh);
        _subQueryHandles.Clear();

        if (_handle != 0)
        {
            NativeMethods.zvec_multi_query_destroy(_handle);
            _handle = 0;
        }

        GC.SuppressFinalize(this);
    }

    ~MultiQuery()
    {
        if (!_disposed) Dispose();
    }
}
