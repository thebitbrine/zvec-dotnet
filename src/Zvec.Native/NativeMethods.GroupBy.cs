using System.Runtime.InteropServices;

namespace Zvec.Native;

// One group returned by zvec_collection_group_by_query. The whole array is
// released with zvec_group_results_free - never free members one by one.
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct GroupResult
{
    public nint GroupByValue; // char*, string form of the grouping value
    public nint* Docs;
    public nuint DocCount;
}

internal static partial class NativeMethods
{
    // =========================================================================
    // Grouped vector query (zvec v0.8.1+)
    // =========================================================================

    // Defaults to at most 2 groups and 3 documents per group
    [LibraryImport(LibName, EntryPoint = "zvec_group_by_vector_query_create")]
    internal static partial nint zvec_group_by_vector_query_create();

    [LibraryImport(LibName, EntryPoint = "zvec_group_by_vector_query_destroy")]
    internal static partial void zvec_group_by_vector_query_destroy(nint query);

    [LibraryImport(LibName, EntryPoint = "zvec_group_by_vector_query_set_field_name", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_group_by_vector_query_set_field_name(nint query, string fieldName);

    [LibraryImport(LibName, EntryPoint = "zvec_group_by_vector_query_set_group_by_field_name", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_group_by_vector_query_set_group_by_field_name(nint query, string fieldName);

    [LibraryImport(LibName, EntryPoint = "zvec_group_by_vector_query_set_group_count")]
    internal static partial uint zvec_group_by_vector_query_set_group_count(nint query, uint count);

    [LibraryImport(LibName, EntryPoint = "zvec_group_by_vector_query_set_topk_per_group")]
    internal static partial uint zvec_group_by_vector_query_set_topk_per_group(nint query, uint topkPerGroup);

    [LibraryImport(LibName, EntryPoint = "zvec_group_by_vector_query_set_query_vector")]
    internal static unsafe partial uint zvec_group_by_vector_query_set_query_vector(nint query, void* data, nuint size);

    [LibraryImport(LibName, EntryPoint = "zvec_group_by_vector_query_set_filter", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_group_by_vector_query_set_filter(nint query, string filter);

    // results is NULL / count 0 when nothing matches
    [LibraryImport(LibName, EntryPoint = "zvec_collection_group_by_query")]
    internal static unsafe partial uint zvec_collection_group_by_query(
        nint collection, nint query, out GroupResult* results, out nuint resultCount);

    [LibraryImport(LibName, EntryPoint = "zvec_group_results_free")]
    internal static unsafe partial void zvec_group_results_free(GroupResult* results, nuint resultCount);
}
