using System.Runtime.InteropServices;

namespace Zvec.Native;

internal static partial class NativeMethods
{
    // =========================================================================
    // Reranker (v0.5.0+ API -- set directly on multi-query, no standalone objects)
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_multi_query_set_rerank_rrf")]
    internal static partial uint zvec_multi_query_set_rerank_rrf(nint query, int rankConstant);

    [LibraryImport(LibName, EntryPoint = "zvec_multi_query_set_rerank_weighted")]
    internal static unsafe partial uint zvec_multi_query_set_rerank_weighted(
        nint query, double* weights, nuint weightCount);

    // =========================================================================
    // Multi-query
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_multi_query_create")]
    internal static partial nint zvec_multi_query_create();

    [LibraryImport(LibName, EntryPoint = "zvec_multi_query_destroy")]
    internal static partial void zvec_multi_query_destroy(nint query);

    [LibraryImport(LibName, EntryPoint = "zvec_multi_query_add_sub_query")]
    internal static partial uint zvec_multi_query_add_sub_query(nint query, nint subQuery);

    [LibraryImport(LibName, EntryPoint = "zvec_multi_query_set_topk")]
    internal static partial uint zvec_multi_query_set_topk(nint query, int topk);

    [LibraryImport(LibName, EntryPoint = "zvec_multi_query_set_filter", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_multi_query_set_filter(nint query, string filter);


    // =========================================================================
    // Sub-query
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_sub_query_create")]
    internal static partial nint zvec_sub_query_create();

    [LibraryImport(LibName, EntryPoint = "zvec_sub_query_destroy")]
    internal static partial void zvec_sub_query_destroy(nint query);

    [LibraryImport(LibName, EntryPoint = "zvec_sub_query_set_field_name", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_sub_query_set_field_name(nint query, string fieldName);

    [LibraryImport(LibName, EntryPoint = "zvec_sub_query_set_num_candidates")]
    internal static partial uint zvec_sub_query_set_num_candidates(nint query, int numCandidates);

    [LibraryImport(LibName, EntryPoint = "zvec_sub_query_set_query_vector")]
    internal static unsafe partial uint zvec_sub_query_set_query_vector(nint query, void* data, nuint size);

    [LibraryImport(LibName, EntryPoint = "zvec_sub_query_set_sparse_vector")]
    internal static unsafe partial uint zvec_sub_query_set_sparse_vector(
        nint query, uint* indices, float* values, nuint count);

    // =========================================================================
    // Multi-query execution
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_collection_multi_query")]
    internal static unsafe partial uint zvec_collection_multi_query(
        nint collection, nint query, out nint* results, out nuint resultCount);
}
