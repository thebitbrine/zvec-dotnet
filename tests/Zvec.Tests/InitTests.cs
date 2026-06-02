namespace Zvec.Tests;

[Collection("Zvec")]
public class InitTests
{
    [Fact]
    public void Initialize_WithDefaults_Succeeds()
    {
        // ZvecFixture already called Initialize -- verify it didn't throw
        Assert.True(ZvecRuntime.IsInitialized);
    }

    [Fact]
    public void Initialize_CalledTwice_DoesNotThrow()
    {
        // Should be idempotent
        ZvecRuntime.Initialize();
        Assert.True(ZvecRuntime.IsInitialized);
    }

    [Fact]
    public void GetVersion_ReturnsNonEmptyString()
    {
        string version = ZvecRuntime.Version;
        Assert.NotNull(version);
        Assert.NotEmpty(version);
    }

    [Fact]
    public void GetVersion_ContainsVersionNumber()
    {
        // Version format: "{base_version}[-{git_info}] (built {build_time})"
        // Should contain something like "0.4.0"
        string version = ZvecRuntime.Version;
        Assert.Contains("0.", version);
    }

    [Fact]
    public void IsInitialized_AfterInit_ReturnsTrue()
    {
        Assert.True(NativeMethods.zvec_is_initialized());
    }

    [Fact]
    public void NativeGetVersion_ReturnsDifferentFromZero()
    {
        nint ptr = NativeMethods.zvec_get_version();
        Assert.NotEqual(0, ptr);
    }
}
