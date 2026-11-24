using System.Runtime.InteropServices;

namespace Zvec.Native;

internal static partial class NativeMethods
{
    // =========================================================================
    // Vector query
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_vector_query_create")]
    internal static partial nint zvec_vector_query_create();

    [LibraryImport(LibName, EntryPoint = "zvec_vector_query_destroy")]
    internal static partial void zvec_vector_query_destroy(nint query);

    [LibraryImport(LibName, EntryPoint = "zvec_vector_query_set_topk")]
    internal static partial uint zvec_vector_query_set_topk(nint query, int topk);

    [LibraryImport(LibName, EntryPoint = "zvec_vector_query_set_field_name", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_vector_query_set_field_name(nint query, string fieldName);

    // void* data, size_t size (size in bytes)
    [LibraryImport(LibName, EntryPoint = "zvec_vector_query_set_query_vector")]
    internal static unsafe partial uint zvec_vector_query_set_query_vector(nint query, void* data, nuint size);

    [LibraryImport(LibName, EntryPoint = "zvec_vector_query_set_filter", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_vector_query_set_filter(nint query, string filter);

    // =========================================================================
    // FTS (full text search)
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_fts_create")]
    internal static partial nint zvec_fts_create();

    [LibraryImport(LibName, EntryPoint = "zvec_fts_destroy")]
    internal static partial void zvec_fts_destroy(nint fts);

    [LibraryImport(LibName, EntryPoint = "zvec_fts_set_query_string", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_fts_set_query_string(nint fts, string queryString);

    [LibraryImport(LibName, EntryPoint = "zvec_fts_set_match_string", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_fts_set_match_string(nint fts, string matchString);

    // payload is copied, caller retains ownership of fts
    [LibraryImport(LibName, EntryPoint = "zvec_vector_query_set_fts")]
    internal static partial uint zvec_vector_query_set_fts(nint query, nint fts);

    // zvec_fts_query_params_t* zvec_query_params_fts_create(const char *default_operator)
    [LibraryImport(LibName, EntryPoint = "zvec_query_params_fts_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint zvec_query_params_fts_create(string defaultOperator);

    [LibraryImport(LibName, EntryPoint = "zvec_query_params_fts_destroy")]
    internal static partial void zvec_query_params_fts_destroy(nint ftsParams);

    // TAKES OWNERSHIP of fts_params on success (c_api.h: "Set FTS query parameters (takes ownership)")
    [LibraryImport(LibName, EntryPoint = "zvec_vector_query_set_fts_params")]
    internal static partial uint zvec_vector_query_set_fts_params(nint query, nint ftsParams);

    // =========================================================================
    // HNSW query params (search-time ef)
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_query_params_hnsw_create")]
    internal static partial nint zvec_query_params_hnsw_create(
        int ef, float radius, [MarshalAs(UnmanagedType.U1)] bool isLinear,
        [MarshalAs(UnmanagedType.U1)] bool isUsingRefiner);

    [LibraryImport(LibName, EntryPoint = "zvec_query_params_hnsw_destroy")]
    internal static partial void zvec_query_params_hnsw_destroy(nint hnswParams);

    // TAKES OWNERSHIP of hnsw_params on success
    [LibraryImport(LibName, EntryPoint = "zvec_vector_query_set_hnsw_params")]
    internal static partial uint zvec_vector_query_set_hnsw_params(nint query, nint hnswParams);

    // =========================================================================
    // Query execution
    // =========================================================================

    // zvec_error_code_t zvec_collection_query(collection*, query*, results***, result_count*)
    [LibraryImport(LibName, EntryPoint = "zvec_collection_query")]
    internal static unsafe partial uint zvec_collection_query(
        nint collection, nint query, out nint* results, out nuint resultCount);

    // =========================================================================
    // Doc result accessors
    // =========================================================================

    // Returns internal pointer, do NOT free
    [LibraryImport(LibName, EntryPoint = "zvec_doc_get_pk_pointer")]
    internal static partial nint zvec_doc_get_pk_pointer(nint doc);

    [LibraryImport(LibName, EntryPoint = "zvec_doc_get_score")]
    internal static partial float zvec_doc_get_score(nint doc);

    [LibraryImport(LibName, EntryPoint = "zvec_doc_get_field_value_basic", StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial uint zvec_doc_get_field_value_basic(
        nint doc, string fieldName, uint fieldType, void* valueBuffer, nuint bufferSize);
}
