using System.Runtime.InteropServices;

namespace Zvec.Native.Handles;

internal class CollectionHandle : SafeHandle
{
    public CollectionHandle() : base(0, true) { }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        NativeMethods.zvec_collection_close(handle);
        return true;
    }
}
