using System.Runtime.InteropServices;

namespace Zvec.Native;

internal static partial class NativeMethods
{
    // =========================================================================
    // Fetch by primary keys + doc field accessors
    // =========================================================================

    // zvec_error_code_t zvec_collection_fetch(collection*, const char* const* pks, size_t count,
    //     const char* const* output_fields, size_t output_field_count, bool include_vector,
    //     zvec_doc_t*** documents, size_t* found_count)
    // documents must be freed with zvec_docs_free.
    [LibraryImport(LibName, EntryPoint = "zvec_collection_fetch")]
    internal static unsafe partial uint zvec_collection_fetch(
        nint collection, nint* primaryKeys, nuint count,
        nint* outputFields, nuint outputFieldCount,
        [MarshalAs(UnmanagedType.U1)] bool includeVector,
        out nint* documents, out nuint foundCount);

    // Pointer stays inside the doc; valid until the doc is freed.
    // STRING returns a null-terminated C string.
    [LibraryImport(LibName, EntryPoint = "zvec_doc_get_field_value_pointer", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_doc_get_field_value_pointer(
        nint doc, string fieldName, uint fieldType, out nint value, out nuint valueSize);

    // =========================================================================
    // Full scan iterator (snapshot of the collection at creation time)
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_iterator_options_create")]
    internal static partial nint zvec_iterator_options_create();

    [LibraryImport(LibName, EntryPoint = "zvec_iterator_options_destroy")]
    internal static partial void zvec_iterator_options_destroy(nint options);

    // output_fields NULL = all fields
    [LibraryImport(LibName, EntryPoint = "zvec_iterator_options_set_output_fields")]
    internal static unsafe partial uint zvec_iterator_options_set_output_fields(
        nint options, nint* outputFields, nuint count);

    [LibraryImport(LibName, EntryPoint = "zvec_iterator_options_set_include_vector")]
    internal static partial uint zvec_iterator_options_set_include_vector(
        nint options, [MarshalAs(UnmanagedType.U1)] bool include);

    // options can be 0 for defaults. Close the iterator before the last collection
    // handle is released - zvec_collection_close waits for open iterators.
    [LibraryImport(LibName, EntryPoint = "zvec_collection_create_iterator")]
    internal static partial uint zvec_collection_create_iterator(nint collection, nint options, out nint iterator);

    // out_doc is 0 at the end; each returned doc is freed with zvec_doc_destroy
    [LibraryImport(LibName, EntryPoint = "zvec_doc_iterator_next")]
    internal static partial uint zvec_doc_iterator_next(nint iterator, out nint doc);

    [LibraryImport(LibName, EntryPoint = "zvec_doc_iterator_close")]
    internal static partial void zvec_doc_iterator_close(nint iterator);
}
