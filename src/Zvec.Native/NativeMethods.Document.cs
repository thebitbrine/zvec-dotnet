using System.Runtime.InteropServices;

namespace Zvec.Native;

internal static partial class NativeMethods
{
    // =========================================================================
    // Document lifecycle
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_doc_create")]
    internal static partial nint zvec_doc_create();

    [LibraryImport(LibName, EntryPoint = "zvec_doc_destroy")]
    internal static partial void zvec_doc_destroy(nint doc);

    [LibraryImport(LibName, EntryPoint = "zvec_doc_set_pk", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void zvec_doc_set_pk(nint doc, string pk);

    // Generic field setter: value is void*, valueSize is byte count
    [LibraryImport(LibName, EntryPoint = "zvec_doc_add_field_by_value", StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial uint zvec_doc_add_field_by_value(
        nint doc, string fieldName, uint dataType, void* value, nuint valueSize);

    // Batch free for result doc arrays
    [LibraryImport(LibName, EntryPoint = "zvec_docs_free")]
    internal static partial void zvec_docs_free(nint docs, nuint count);

    // =========================================================================
    // DML -- will be filled in Block 4
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_collection_insert")]
    internal static unsafe partial uint zvec_collection_insert(
        nint collection, nint* docs, nuint docCount,
        out nuint successCount, out nuint errorCount);

    [LibraryImport(LibName, EntryPoint = "zvec_collection_upsert")]
    internal static unsafe partial uint zvec_collection_upsert(
        nint collection, nint* docs, nuint docCount,
        out nuint successCount, out nuint errorCount);

    [LibraryImport(LibName, EntryPoint = "zvec_collection_update")]
    internal static unsafe partial uint zvec_collection_update(
        nint collection, nint* docs, nuint docCount,
        out nuint successCount, out nuint errorCount);

    [LibraryImport(LibName, EntryPoint = "zvec_collection_delete", StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial uint zvec_collection_delete(
        nint collection, nint* pks, nuint pkCount,
        out nuint successCount, out nuint errorCount);
}
