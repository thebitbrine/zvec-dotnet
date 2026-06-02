using System.Runtime.InteropServices;

namespace Zvec.Native.Handles;

internal class FieldSchemaHandle : SafeHandle
{
    public FieldSchemaHandle() : base(0, true) { }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        NativeMethods.zvec_field_schema_destroy(handle);
        return true;
    }
}
