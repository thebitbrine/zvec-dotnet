using System.Runtime.InteropServices;

namespace Zvec.Native;

internal static partial class NativeMethods
{
    // =========================================================================
    // Field schema
    // =========================================================================

    // zvec_field_schema_t* zvec_field_schema_create(name, data_type, nullable, dimension)
    // For scalar fields: dimension = 0
    [LibraryImport(LibName, EntryPoint = "zvec_field_schema_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint zvec_field_schema_create(
        string name, uint dataType, [MarshalAs(UnmanagedType.U1)] bool nullable, uint dimension);

    // void zvec_field_schema_destroy(zvec_field_schema_t *schema)
    [LibraryImport(LibName, EntryPoint = "zvec_field_schema_destroy")]
    internal static partial void zvec_field_schema_destroy(nint schema);

    // zvec_error_code_t zvec_field_schema_set_index_params(field*, index_params*)
    // index_params is copied, caller retains ownership
    [LibraryImport(LibName, EntryPoint = "zvec_field_schema_set_index_params")]
    internal static partial uint zvec_field_schema_set_index_params(nint schema, nint indexParams);

    // =========================================================================
    // Index params
    // =========================================================================

    // zvec_index_params_t* zvec_index_params_create(zvec_index_type_t index_type)
    [LibraryImport(LibName, EntryPoint = "zvec_index_params_create")]
    internal static partial nint zvec_index_params_create(uint indexType);

    // void zvec_index_params_destroy(zvec_index_params_t *params)
    [LibraryImport(LibName, EntryPoint = "zvec_index_params_destroy")]
    internal static partial void zvec_index_params_destroy(nint indexParams);

    // zvec_error_code_t zvec_index_params_set_metric_type(params*, metric_type)
    [LibraryImport(LibName, EntryPoint = "zvec_index_params_set_metric_type")]
    internal static partial uint zvec_index_params_set_metric_type(nint indexParams, uint metricType);

    // zvec_error_code_t zvec_index_params_set_hnsw_params(params*, m, ef_construction)
    [LibraryImport(LibName, EntryPoint = "zvec_index_params_set_hnsw_params")]
    internal static partial uint zvec_index_params_set_hnsw_params(nint indexParams, int m, int efConstruction);

    // zvec_error_code_t zvec_index_params_set_quantize_type(params*, quantize_type)
    [LibraryImport(LibName, EntryPoint = "zvec_index_params_set_quantize_type")]
    internal static partial uint zvec_index_params_set_quantize_type(nint indexParams, uint quantizeType);

    // zvec_error_code_t zvec_index_params_set_ivf_params(params*, n_list, n_iters, use_soar)
    [LibraryImport(LibName, EntryPoint = "zvec_index_params_set_ivf_params")]
    internal static partial uint zvec_index_params_set_ivf_params(
        nint indexParams, int nList, int nIters, [MarshalAs(UnmanagedType.U1)] bool useSoar);

    // =========================================================================
    // Collection schema
    // =========================================================================

    // zvec_collection_schema_t* zvec_collection_schema_create(const char *name)
    [LibraryImport(LibName, EntryPoint = "zvec_collection_schema_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint zvec_collection_schema_create(string name);

    // void zvec_collection_schema_destroy(zvec_collection_schema_t *schema)
    [LibraryImport(LibName, EntryPoint = "zvec_collection_schema_destroy")]
    internal static partial void zvec_collection_schema_destroy(nint schema);

    // zvec_error_code_t zvec_collection_schema_add_field(schema*, field*)
    // field is CLONED, caller retains ownership
    [LibraryImport(LibName, EntryPoint = "zvec_collection_schema_add_field")]
    internal static partial uint zvec_collection_schema_add_field(nint schema, nint field);
}
