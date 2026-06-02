using System.Runtime.InteropServices;

namespace Zvec.Native.Handles;

internal class IndexParamsHandle : SafeHandle
{
    public IndexParamsHandle() : base(0, true) { }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        NativeMethods.zvec_index_params_destroy(handle);
        return true;
    }
}
