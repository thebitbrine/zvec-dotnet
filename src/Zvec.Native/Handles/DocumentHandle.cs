using System.Runtime.InteropServices;

namespace Zvec.Native.Handles;

internal class DocumentHandle : SafeHandle
{
    public DocumentHandle() : base(0, true) { }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        NativeMethods.zvec_doc_destroy(handle);
        return true;
    }
}
