using System.Runtime.InteropServices;

namespace Zvec.Native.Handles;

internal class CollectionOptionsHandle : SafeHandle
{
    public CollectionOptionsHandle() : base(0, true) { }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        NativeMethods.zvec_collection_options_destroy(handle);
        return true;
    }
}
