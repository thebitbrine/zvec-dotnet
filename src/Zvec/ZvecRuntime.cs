using System.Runtime.InteropServices;
using Zvec.Native;

namespace Zvec;

/// <summary>
/// Global zvec library lifecycle. Must call Initialize() before any operations.
/// </summary>
public static class ZvecRuntime
{
    private static readonly object _lock = new();
    private static bool _initialized;

    /// <summary>
    /// Initialize the zvec library with default configuration.
    /// Safe to call multiple times -- only the first call does anything.
    /// </summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized)
                return;

            ZvecError.ThrowIfFailed(NativeMethods.zvec_initialize(0));
            _initialized = true;
        }
    }

    /// <summary>
    /// Shutdown the zvec library and release global resources.
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            if (!_initialized)
                return;

            ZvecError.ThrowIfFailed(NativeMethods.zvec_shutdown());
            _initialized = false;
        }
    }

    /// <summary>
    /// Check if the library is initialized.
    /// </summary>
    public static bool IsInitialized => NativeMethods.zvec_is_initialized();

    /// <summary>
    /// Get the native library version string.
    /// </summary>
    public static string Version
    {
        get
        {
            nint ptr = NativeMethods.zvec_get_version();
            return Marshal.PtrToStringUTF8(ptr) ?? "unknown";
        }
    }
}
