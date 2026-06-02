using System.Runtime.InteropServices;

namespace Zvec.Native.Handles;

internal class VectorQueryHandle : SafeHandle
{
    public VectorQueryHandle() : base(0, true) { }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        NativeMethods.zvec_vector_query_destroy(handle);
        return true;
    }
}
