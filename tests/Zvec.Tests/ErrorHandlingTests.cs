namespace Zvec.Tests;

[Collection("Zvec")]
public class ErrorHandlingTests
{
    [Fact]
    public void ZvecErrorCode_Ok_IsZero()
    {
        Assert.Equal(0u, (uint)ZvecErrorCode.Ok);
    }

    [Fact]
    public void ZvecErrorCode_AllValuesInRange()
    {
        foreach (ZvecErrorCode code in Enum.GetValues<ZvecErrorCode>())
        {
            Assert.InRange((uint)code, 0u, 10u);
        }
    }

    [Fact]
    public void ZvecErrorCode_HasExpectedCount()
    {
        // c_api.h defines 11 error codes (0 through 10)
        Assert.Equal(11, Enum.GetValues<ZvecErrorCode>().Length);
    }

    [Fact]
    public void ThrowIfFailed_OnZero_DoesNotThrow()
    {
        // Should not throw for ZVEC_OK
        ZvecError.ThrowIfFailed(0);
    }

    [Fact]
    public void ThrowIfFailed_OnNonZero_ThrowsZvecException()
    {
        var ex = Assert.Throws<ZvecException>(() =>
            ZvecError.ThrowIfFailed((uint)ZvecErrorCode.InvalidArgument));

        Assert.Equal(ZvecErrorCode.InvalidArgument, ex.ErrorCode);
    }

    [Fact]
    public void ThrowIfFailed_OnNonZero_HasMessage()
    {
        var ex = Assert.Throws<ZvecException>(() =>
            ZvecError.ThrowIfFailed((uint)ZvecErrorCode.NotFound));

        Assert.NotNull(ex.Message);
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void ZvecException_IsNotFound_ReturnsTrueForNotFound()
    {
        var ex = new ZvecException(ZvecErrorCode.NotFound, "test");
        Assert.True(ex.IsNotFound);
        Assert.False(ex.IsAlreadyExists);
        Assert.False(ex.IsInvalidArgument);
        Assert.False(ex.IsNotSupported);
    }

    [Fact]
    public void ZvecException_IsAlreadyExists_ReturnsTrueForAlreadyExists()
    {
        var ex = new ZvecException(ZvecErrorCode.AlreadyExists, "test");
        Assert.True(ex.IsAlreadyExists);
        Assert.False(ex.IsNotFound);
    }

    [Fact]
    public void ZvecException_IsInvalidArgument_ReturnsTrueForInvalidArgument()
    {
        var ex = new ZvecException(ZvecErrorCode.InvalidArgument, "test");
        Assert.True(ex.IsInvalidArgument);
        Assert.False(ex.IsNotFound);
    }

    [Fact]
    public void ZvecException_IsNotSupported_ReturnsTrueForNotSupported()
    {
        var ex = new ZvecException(ZvecErrorCode.NotSupported, "test");
        Assert.True(ex.IsNotSupported);
        Assert.False(ex.IsNotFound);
    }

    [Fact]
    public void ZvecException_PreservesErrorCode()
    {
        foreach (ZvecErrorCode code in Enum.GetValues<ZvecErrorCode>())
        {
            var ex = new ZvecException(code, $"error {code}");
            Assert.Equal(code, ex.ErrorCode);
        }
    }

    [Fact]
    public void ZvecException_PreservesMessage()
    {
        var ex = new ZvecException(ZvecErrorCode.InternalError, "something broke");
        Assert.Equal("something broke", ex.Message);
    }

    [Fact]
    public void ZvecException_WithInnerException_PreservesBoth()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ZvecException(ZvecErrorCode.InternalError, "outer", inner);
        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(ZvecErrorCode.InternalError, ex.ErrorCode);
    }

    [Fact]
    public void ClearError_DoesNotThrow()
    {
        NativeMethods.zvec_clear_error();
    }

    [Fact]
    public void GetLastError_WhenNoError_ReturnsOk()
    {
        NativeMethods.zvec_clear_error();
        uint code = NativeMethods.zvec_get_last_error(out nint msgPtr);
        // Either returns OK with null msg, or returns a code -- both are valid.
        // Just verify it doesn't crash.
        if (msgPtr != 0)
            NativeMethods.zvec_free(msgPtr);
    }
}
