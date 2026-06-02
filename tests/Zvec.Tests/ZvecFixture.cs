namespace Zvec.Tests;

/// <summary>
/// Shared fixture that initializes zvec once for the entire test run.
/// All test classes that need zvec should implement IClassFixture&lt;ZvecFixture&gt;.
/// </summary>
public class ZvecFixture : IDisposable
{
    public ZvecFixture()
    {
        ZvecRuntime.Initialize();
    }

    public void Dispose()
    {
        // Don't shutdown here -- other test classes may still need it.
        // zvec handles process exit cleanup internally.
    }
}

[CollectionDefinition("Zvec")]
public class ZvecTestCollection : ICollectionFixture<ZvecFixture>
{
}

