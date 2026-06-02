using System.Runtime.InteropServices;

namespace Zvec.Native;

/// <summary>
/// P/Invoke declarations for zvec c_api.h.
/// Split across partial class files by functional group.
/// </summary>
internal static partial class NativeMethods
{
    private const string LibName = "zvec_c_api";

    // =========================================================================
    // Init / shutdown / version
    // =========================================================================

    // zvec_error_code_t zvec_initialize(const zvec_config_data_t *config)
    // NULL config = use defaults
    [LibraryImport(LibName, EntryPoint = "zvec_initialize")]
    internal static partial uint zvec_initialize(nint config);

    // zvec_error_code_t zvec_shutdown(void)
    [LibraryImport(LibName, EntryPoint = "zvec_shutdown")]
    internal static partial uint zvec_shutdown();

    // bool zvec_is_initialized(void)
    [LibraryImport(LibName, EntryPoint = "zvec_is_initialized")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool zvec_is_initialized();

    // const char* zvec_get_version(void)
    // Returns internal string, do NOT free
    [LibraryImport(LibName, EntryPoint = "zvec_get_version")]
    internal static partial nint zvec_get_version();

    // =========================================================================
    // Error handling
    // =========================================================================

    // zvec_error_code_t zvec_get_last_error(char **error_msg)
    // Allocates error_msg, caller frees with zvec_free
    [LibraryImport(LibName, EntryPoint = "zvec_get_last_error")]
    internal static partial uint zvec_get_last_error(out nint errorMsg);

    // void zvec_clear_error(void)
    [LibraryImport(LibName, EntryPoint = "zvec_clear_error")]
    internal static partial void zvec_clear_error();

    // =========================================================================
    // Memory management
    // =========================================================================

    // void zvec_free(void *ptr)
    [LibraryImport(LibName, EntryPoint = "zvec_free")]
    internal static partial void zvec_free(nint ptr);

    // void* zvec_malloc(size_t size)
    [LibraryImport(LibName, EntryPoint = "zvec_malloc")]
    internal static partial nint zvec_malloc(nuint size);
}
