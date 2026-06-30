using System.Runtime.InteropServices;
using System.Text;
using Zvec.Native;

namespace Zvec;

/// <summary>
/// Represents a document to insert, update, or upsert into a collection.
/// Each document has a primary key and typed field values.
/// Implements IDisposable -- caller must dispose after use. Insert borrows
/// the handle, so the document remains valid after insertion.
/// </summary>
public class ZvecDocument : IDisposable
{
    private nint _handle;
    private bool _disposed;

    /// <summary>
    /// Create a new document with the given primary key.
    /// </summary>
    /// <param name="primaryKey">Unique identifier for this document.</param>
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

    /// <summary>
    /// Set a float32 vector field value.
    /// </summary>
    /// <param name="fieldName">Name of the vector field (must match schema).</param>
    /// <param name="vector">Vector data. Length must match the field's dimension.</param>
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

    /// <summary>Set a string field value.</summary>
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

    /// <summary>Set a 32-bit integer field value.</summary>
    public unsafe void SetInt32(string fieldName, int value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Int32,
                &value, (nuint)sizeof(int)));
    }

    /// <summary>Set a 64-bit integer field value.</summary>
    public unsafe void SetInt64(string fieldName, long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Int64,
                &value, (nuint)sizeof(long)));
    }

    /// <summary>Set a float field value.</summary>
    public unsafe void SetFloat(string fieldName, float value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Float,
                &value, (nuint)sizeof(float)));
    }

    /// <summary>Set a double field value.</summary>
    public unsafe void SetDouble(string fieldName, double value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Double,
                &value, (nuint)sizeof(double)));
    }

    /// <summary>
    /// Set a sparse vector field. Format: [nnz:uint32][indices:uint32[nnz]][values:float[nnz]]
    /// </summary>
    /// <param name="fieldName">Sparse vector field name.</param>
    /// <param name="sparseVector">Non-zero entries as dimension_index -> weight.</param>
    public unsafe void SetSparseVector(string fieldName, Dictionary<uint, float> sparseVector)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int nnz = sparseVector.Count;
        // Format per zvec maintainer: [nnz:size_t][indices:uint32 * nnz][values:float * nnz]
        // size_t is 8 bytes on 64-bit platforms
        int bufferSize = sizeof(nuint) + nnz * sizeof(uint) + nnz * sizeof(float);
        var buffer = new byte[bufferSize];

        fixed (byte* ptr = buffer)
        {
            *(nuint*)ptr = (nuint)nnz;
            uint* indices = (uint*)(ptr + sizeof(nuint));
            float* values = (float*)(ptr + sizeof(nuint) + nnz * sizeof(uint));

            int i = 0;
            foreach (var kv in sparseVector)
            {
                indices[i] = kv.Key;
                values[i] = kv.Value;
                i++;
            }

            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_doc_add_field_by_value(
                    _handle, fieldName, (uint)DataType.SparseVectorFp32,
                    ptr, (nuint)bufferSize));
        }
    }

    /// <summary>
    /// Set a string array field value.
    /// </summary>
    public unsafe void SetStringArray(string fieldName, string[] values)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Serialize as null-terminated C strings concatenated: "str1\0str2\0str3\0"
        // The C code dispatches based on value_size % sizeof(void*):
        //   aligned -> treats as zvec_string_t** (wrong for us)
        //   unaligned -> treats as null-terminated strings (correct)
        // So we pad to ensure the total size is NOT 8-byte aligned.

        var utf8Strings = values.Select(Encoding.UTF8.GetBytes).ToArray();
        int rawSize = utf8Strings.Sum(s => s.Length + 1);

        // Pad if size would be 8-byte aligned
        int paddedSize = rawSize;
        if (paddedSize % sizeof(nint) == 0)
            paddedSize++;

        var buffer = new byte[paddedSize];
        int offset = 0;
        foreach (var s in utf8Strings)
        {
            Array.Copy(s, 0, buffer, offset, s.Length);
            offset += s.Length;
            buffer[offset++] = 0;
        }
        // remaining bytes are already 0 from array init

        fixed (byte* ptr = buffer)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_doc_add_field_by_value(
                    _handle, fieldName, (uint)DataType.ArrayString,
                    ptr, (nuint)paddedSize));
        }
    }

    /// <summary>
    /// Set an int32 array field value.
    /// </summary>
    public unsafe void SetInt32Array(string fieldName, int[] values)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        fixed (int* ptr = values)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_doc_add_field_by_value(
                    _handle, fieldName, (uint)DataType.ArrayInt32,
                    ptr, (nuint)(values.Length * sizeof(int))));
        }
    }

    /// <summary>
    /// Set an int64 array field value.
    /// </summary>
    public unsafe void SetInt64Array(string fieldName, long[] values)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        fixed (long* ptr = values)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_doc_add_field_by_value(
                    _handle, fieldName, (uint)DataType.ArrayInt64,
                    ptr, (nuint)(values.Length * sizeof(long))));
        }
    }

    /// <summary>
    /// Set a float array field value (not a vector -- a list of float scalars).
    /// </summary>
    public unsafe void SetFloatArray(string fieldName, float[] values)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        fixed (float* ptr = values)
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_doc_add_field_by_value(
                    _handle, fieldName, (uint)DataType.ArrayFloat,
                    ptr, (nuint)(values.Length * sizeof(float))));
        }
    }

    /// <summary>Set a boolean field value.</summary>
    public unsafe void SetBool(string fieldName, bool value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte b = value ? (byte)1 : (byte)0;
        ZvecError.ThrowIfFailed(
            NativeMethods.zvec_doc_add_field_by_value(
                _handle, fieldName, (uint)DataType.Bool,
                &b, (nuint)sizeof(byte)));
    }

    /// <inheritdoc/>
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
