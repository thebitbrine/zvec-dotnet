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
