using System.Runtime.InteropServices;
using Zvec.Native;

namespace Zvec;

/// <summary>
/// Result of ZvecCollection.Fetch. Owns the native doc array - all FetchedDoc
/// field pointers are valid ONLY until this is disposed. Read what you need,
/// then dispose (or copy values out; strings/ints returned by getters are
/// managed copies and safe to keep).
/// </summary>
public sealed unsafe class FetchResult : IDisposable
{
    private nint* _docs;
    private nuint _count;
    private bool _disposed;
    private readonly List<FetchedDoc> _items = new();

    internal FetchResult(nint* docs, nuint count)
    {
        _docs = docs;
        _count = count;
        for (nuint i = 0; i < count; i++)
            _items.Add(new FetchedDoc(this, docs[i]));
    }

    public IReadOnlyList<FetchedDoc> Docs
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _items;
        }
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_docs != null)
        {
            NativeMethods.zvec_docs_free((nint)_docs, _count);
            _docs = null;
        }
        GC.SuppressFinalize(this);
    }

    ~FetchResult()
    {
        if (!_disposed && _docs != null)
        {
            NativeMethods.zvec_docs_free((nint)_docs, _count);
            _docs = null;
        }
    }
}

/// <summary>
/// One document read back from a collection. Getters return managed copies (safe
/// to keep); the doc itself is only readable while the parent FetchResult is
/// alive, or for Scan() until the enumeration moves on.
/// </summary>
public sealed class FetchedDoc
{
    // null for Scan() docs, which own their native handle one by one
    private readonly FetchResult? _owner;
    private nint _handle;

    internal FetchedDoc(FetchResult? owner, nint handle)
    {
        _owner = owner;
        _handle = handle;
    }

    // Scan() docs only: free the native doc once the enumeration has moved past it.
    internal void Release()
    {
        if (_handle == 0) return;
        NativeMethods.zvec_doc_destroy(_handle);
        _handle = 0;
    }

    private void ThrowIfGone()
    {
        _owner?.ThrowIfDisposed();
        ObjectDisposedException.ThrowIf(_handle == 0, this);
    }

    public string Pk
    {
        get
        {
            ThrowIfGone();
            nint p = NativeMethods.zvec_doc_get_pk_pointer(_handle);
            return Marshal.PtrToStringUTF8(p) ?? "";
        }
    }

    // Absent/valueless fields are detected via the getter's own error code, so
    // one native call both probes and reads.
    private bool TryGetPointer(string field, DataType type, out nint value)
    {
        ThrowIfGone();
        uint rc = NativeMethods.zvec_doc_get_field_value_pointer(
            _handle, field, (uint)type, out value, out _);
        if (rc == 0) return true;
        // absent field / no value / not in output_fields -> null, anything else -> throw
        var code = (ZvecErrorCode)rc;
        if (code is ZvecErrorCode.NotFound or ZvecErrorCode.InvalidArgument or ZvecErrorCode.FailedPrecondition)
            return false;
        ZvecError.ThrowIfFailed(rc);
        return false; // unreachable
    }

    public string? GetString(string field)
        => TryGetPointer(field, DataType.String, out nint v) ? Marshal.PtrToStringUTF8(v) : null;

    public long? GetInt64(string field)
        => TryGetPointer(field, DataType.Int64, out nint v) ? Marshal.ReadInt64(v) : null;

    public int? GetInt32(string field)
        => TryGetPointer(field, DataType.Int32, out nint v) ? Marshal.ReadInt32(v) : null;

    public double? GetDouble(string field)
    {
        if (!TryGetPointer(field, DataType.Double, out nint v)) return null;
        double[] buf = new double[1];
        Marshal.Copy(v, buf, 0, 1);
        return buf[0];
    }

    /// <summary>Read a float32 vector field. Requires includeVector: true on Fetch/Scan.</summary>
    public float[]? GetVector(string field)
    {
        ThrowIfGone();
        uint rc = NativeMethods.zvec_doc_get_field_value_pointer(
            _handle, field, (uint)DataType.VectorFp32, out nint value, out nuint size);
        if (rc != 0) return null;
        var vec = new float[(int)size / sizeof(float)];
        Marshal.Copy(value, vec, 0, vec.Length);
        return vec;
    }
}
