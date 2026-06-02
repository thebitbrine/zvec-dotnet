using Zvec.Native;

namespace Zvec;

/// <summary>
/// Fluent schema builder used via Action in ZvecCollection.CreateAndOpen.
/// Manages native handle lifecycle internally.
/// </summary>
public class ZvecSchema
{
    private readonly nint _schemaHandle;

    internal ZvecSchema(nint schemaHandle)
    {
        _schemaHandle = schemaHandle;
    }

    internal nint Handle => _schemaHandle;

    /// <summary>
    /// Add a dense vector field with index configuration.
    /// </summary>
    public void AddVector(string name, uint dimensions,
        MetricType metric = MetricType.Cosine,
        IndexType indexType = IndexType.Hnsw)
    {
        nint fieldHandle = NativeMethods.zvec_field_schema_create(
            name, (uint)DataType.VectorFp32, false, dimensions);
        if (fieldHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create vector field schema");

        try
        {
            // Create index params, set metric, attach to field
            nint paramsHandle = NativeMethods.zvec_index_params_create((uint)indexType);
            if (paramsHandle == 0)
                throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create index params");

            try
            {
                ZvecError.ThrowIfFailed(
                    NativeMethods.zvec_index_params_set_metric_type(paramsHandle, (uint)metric));

                // set_index_params copies, so we still own paramsHandle
                ZvecError.ThrowIfFailed(
                    NativeMethods.zvec_field_schema_set_index_params(fieldHandle, paramsHandle));
            }
            finally
            {
                NativeMethods.zvec_index_params_destroy(paramsHandle);
            }

            // add_field clones, so we still own fieldHandle
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_schema_add_field(_schemaHandle, fieldHandle));
        }
        finally
        {
            NativeMethods.zvec_field_schema_destroy(fieldHandle);
        }
    }

    /// <summary>
    /// Add a sparse vector field for BM25 or learned sparse embeddings.
    /// </summary>
    public void AddSparseVector(string name, MetricType metric = MetricType.InnerProduct,
        IndexType indexType = IndexType.Hnsw)
    {
        nint fieldHandle = NativeMethods.zvec_field_schema_create(
            name, (uint)DataType.SparseVectorFp32, false, 0);
        if (fieldHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create sparse vector field schema");

        try
        {
            nint paramsHandle = NativeMethods.zvec_index_params_create((uint)indexType);
            if (paramsHandle == 0)
                throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create index params");

            try
            {
                ZvecError.ThrowIfFailed(
                    NativeMethods.zvec_index_params_set_metric_type(paramsHandle, (uint)metric));
                ZvecError.ThrowIfFailed(
                    NativeMethods.zvec_field_schema_set_index_params(fieldHandle, paramsHandle));
            }
            finally
            {
                NativeMethods.zvec_index_params_destroy(paramsHandle);
            }

            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_schema_add_field(_schemaHandle, fieldHandle));
        }
        finally
        {
            NativeMethods.zvec_field_schema_destroy(fieldHandle);
        }
    }

    /// <summary>
    /// Add a scalar field (string, int32, float, etc).
    /// </summary>
    public void AddScalar(string name, DataType dataType, bool nullable = false)
    {
        nint fieldHandle = NativeMethods.zvec_field_schema_create(
            name, (uint)dataType, nullable, 0);
        if (fieldHandle == 0)
            throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create scalar field schema");

        try
        {
            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_collection_schema_add_field(_schemaHandle, fieldHandle));
        }
        finally
        {
            NativeMethods.zvec_field_schema_destroy(fieldHandle);
        }
    }
}
