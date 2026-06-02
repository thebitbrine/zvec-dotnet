using System.Runtime.InteropServices;
using Zvec.Native;

namespace Zvec;

internal static class ZvecError
{
    internal static void ThrowIfFailed(uint errorCode)
    {
        if (errorCode == 0)
            return;

        string message = $"zvec error {errorCode}";

        NativeMethods.zvec_get_last_error(out nint msgPtr);
        if (msgPtr != 0)
        {
            message = Marshal.PtrToStringUTF8(msgPtr) ?? message;
            NativeMethods.zvec_free(msgPtr);
        }

        throw new ZvecException((ZvecErrorCode)errorCode, message);
    }
}
