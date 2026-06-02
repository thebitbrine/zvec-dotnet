using Zvec.Native;

namespace Zvec;

public class ZvecException : Exception
{
    public ZvecErrorCode ErrorCode { get; }

    public ZvecException(ZvecErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public ZvecException(ZvecErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public bool IsNotFound => ErrorCode == ZvecErrorCode.NotFound;
    public bool IsAlreadyExists => ErrorCode == ZvecErrorCode.AlreadyExists;
    public bool IsInvalidArgument => ErrorCode == ZvecErrorCode.InvalidArgument;
    public bool IsNotSupported => ErrorCode == ZvecErrorCode.NotSupported;
}
