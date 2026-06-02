using System.Runtime.InteropServices;

namespace Zvec.Native.Handles;

internal class CollectionSchemaHandle : SafeHandle
{
    public CollectionSchemaHandle() : base(0, true) { }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        NativeMethods.zvec_collection_schema_destroy(handle);
        return true;
    }
}
