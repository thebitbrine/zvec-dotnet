namespace Zvec.Native;

/// <summary>
/// Maps zvec_error_code_t from c_api.h (values 0-10).
/// </summary>
public enum ZvecErrorCode : uint
{
    Ok                 = 0,
    NotFound           = 1,
    AlreadyExists      = 2,
    InvalidArgument    = 3,
    PermissionDenied   = 4,
    FailedPrecondition = 5,
    ResourceExhausted  = 6,
    Unavailable        = 7,
    InternalError      = 8,
    NotSupported       = 9,
    Unknown            = 10,
}
