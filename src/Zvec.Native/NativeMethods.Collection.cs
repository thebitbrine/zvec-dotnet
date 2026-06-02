using System.Runtime.InteropServices;

namespace Zvec.Native;

internal static partial class NativeMethods
{
    // =========================================================================
    // Collection options
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_collection_options_create")]
    internal static partial nint zvec_collection_options_create();

    [LibraryImport(LibName, EntryPoint = "zvec_collection_options_destroy")]
    internal static partial void zvec_collection_options_destroy(nint options);

    // =========================================================================
    // Collection lifecycle
    // =========================================================================

    // zvec_error_code_t zvec_collection_create_and_open(path, schema*, options*, collection**)
    [LibraryImport(LibName, EntryPoint = "zvec_collection_create_and_open", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_collection_create_and_open(
        string path, nint schema, nint options, out nint collection);

    // zvec_error_code_t zvec_collection_open(path, options*, collection**)
    [LibraryImport(LibName, EntryPoint = "zvec_collection_open", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_collection_open(string path, nint options, out nint collection);

    [LibraryImport(LibName, EntryPoint = "zvec_collection_close")]
    internal static partial uint zvec_collection_close(nint collection);

    [LibraryImport(LibName, EntryPoint = "zvec_collection_destroy")]
    internal static partial uint zvec_collection_destroy(nint collection);

    [LibraryImport(LibName, EntryPoint = "zvec_collection_flush")]
    internal static partial uint zvec_collection_flush(nint collection);

    // =========================================================================
    // Index management
    // =========================================================================

    [LibraryImport(LibName, EntryPoint = "zvec_collection_create_index", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint zvec_collection_create_index(nint collection, string fieldName, nint indexParams);

    [LibraryImport(LibName, EntryPoint = "zvec_collection_optimize")]
    internal static partial uint zvec_collection_optimize(nint collection);
}
