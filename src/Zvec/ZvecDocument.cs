using System.Runtime.InteropServices;
using System.Text;
using Zvec.Native;

namespace Zvec;

/// <summary>
/// Document builder. Wraps a native zvec_doc_t handle.
/// Insert borrows the handle (const), so doc remains valid after insert and must be disposed.
/// </summary>
public class ZvecDocument : IDisposable
{
    private nint _handle;
    private bool _disposed;

    public ZvecDocument(string primaryKey)
    {
        _handle = NativeMethods.zvec_doc_create();
        if (_handle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create document");

        NativeMethods.zvec_doc_set_pk(_handle, primaryKey);
    }

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    public unsafe void SetVector(string fieldName, float[] vector)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        fixed (float* ptr = vector)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_doc_add_field_by_value(
                    _handle, fieldName, (uint)DataType.VectorFp32,
                    ptr, (nuint)(vector.Length * sizeof(float))));
        }
    }

    public unsafe void SetString(string fieldName, string value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        fixed (byte* ptr = utf8)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_doc_add_field_by_value(
                    _handle, fieldName, (uint)DataType.String,
                    ptr, (nuint)utf8.Length));
        }
    }

    public unsafe void SetInt32(string fieldName, int value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Int32,
                &value, (nuint)sizeof(int)));
    }

    public unsafe void SetInt64(string fieldName, long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Int64,
                &value, (nuint)sizeof(long)));
    }

    public unsafe void SetFloat(string fieldName, float value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Float,
                &value, (nuint)sizeof(float)));
    }

    public unsafe void SetDouble(string fieldName, double value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Double,
                &value, (nuint)sizeof(double)));
    }

    public unsafe void SetBool(string fieldName, bool value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte b = value ? (byte)1 : (byte)0;
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Bool,
                &b, (nuint)sizeof(byte)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != 0)
        {
            NativeMethods.zvec_doc_destroy(_handle);
            _handle = 0;
        }

        GC.SuppressFinalize(this);
    }

    ~ZvecDocument()
    {
        if (_handle != 0)
        {
            NativeMethods.zvec_doc_destroy(_handle);
            _handle = 0;
        }
    }
}
