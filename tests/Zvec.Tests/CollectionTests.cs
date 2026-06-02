namespace Zvec.Tests;

[Collection("Zvec")]
public class CollectionTests : IDisposable
{
    private readonly string _tempDir;

    public CollectionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zvec_tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CollPath(string name = "test_col") => Path.Combine(_tempDir, name);

    // =========================================================================
    // CreateAndOpen
    // =========================================================================

    [Fact]
    public void CreateAndOpen_WithValidSchema_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 64, MetricType.Cosine);
        });

        Assert.NotNull(col);
    }

    [Fact]
    public void CreateAndOpen_WithMultipleFields_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("embedding", 128, MetricType.Cosine);
            schema.AddScalar("title", DataType.String);
            schema.AddScalar("year", DataType.Int32);
        });

        Assert.NotNull(col);
    }

    [Fact]
    public void CreateAndOpen_SmallDimension_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 1);
        });

        Assert.NotNull(col);
    }

    [Fact]
    public void CreateAndOpen_LargeDimension_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 2048);
        });

        Assert.NotNull(col);
    }

    [Fact]
    public void CreateAndOpen_DuplicatePath_Throws()
    {
        string path = CollPath("dup");

        using var col1 = ZvecCollection.CreateAndOpen(path, schema =>
        {
            schema.AddVector("vec", 64);
        });
        col1.Close();

        // Opening with create_and_open on existing path should fail
        var ex = Assert.Throws<ZvecException>(() =>
        {
            using var col2 = ZvecCollection.CreateAndOpen(path, schema =>
            {
                schema.AddVector("vec", 64);
            });
        });

        // zvec returns InvalidArgument for duplicate paths
        Assert.Equal(ZvecErrorCode.InvalidArgument, ex.ErrorCode);
    }

    // =========================================================================
    // Open existing
    // =========================================================================

    [Fact]
    public void Open_ExistingCollection_Succeeds()
    {
        string path = CollPath("reopen");

        // Create and close
        using (var col = ZvecCollection.CreateAndOpen(path, schema =>
        {
            schema.AddVector("vec", 64, MetricType.Cosine);
            schema.AddScalar("title", DataType.String);
        }))
        {
            col.Flush();
        }

        // Reopen
        using var col2 = ZvecCollection.Open(path);
        Assert.NotNull(col2);
    }

    [Fact]
    public void Open_NonExistentPath_Throws()
    {
        string path = CollPath("does_not_exist");

        var ex = Assert.Throws<ZvecException>(() =>
        {
            using var col = ZvecCollection.Open(path);
        });

        // Should be NotFound or similar
        Assert.NotEqual(ZvecErrorCode.Ok, ex.ErrorCode);
    }

    // =========================================================================
    // Flush
    // =========================================================================

    [Fact]
    public void Flush_OnOpenCollection_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 64);
        });

        col.Flush();
    }

    // =========================================================================
    // Dispose / Close
    // =========================================================================

    [Fact]
    public void Dispose_ThenAccess_ThrowsObjectDisposed()
    {
        var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 64);
        });

        col.Dispose();

        Assert.Throws<ObjectDisposedException>(() => col.Flush());
    }

    [Fact]
    public void DoubleDispose_DoesNotThrow()
    {
        var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 64);
        });

        col.Dispose();
        col.Dispose(); // should not throw
    }

    [Fact]
    public void Close_ThenAccess_ThrowsObjectDisposed()
    {
        var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 64);
        });

        col.Close();

        Assert.Throws<ObjectDisposedException>(() => col.Flush());
    }

    [Fact]
    public void UsingBlock_DisposesCorrectly()
    {
        ZvecCollection? colRef;
        using (var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 64);
        }))
        {
            colRef = col;
            col.Flush(); // should work inside using
        }

        Assert.Throws<ObjectDisposedException>(() => colRef.Flush());
    }

    // =========================================================================
    // Multiple collections
    // =========================================================================

    [Fact]
    public void MultipleCollections_SimultaneouslyOpen_Succeed()
    {
        using var col1 = ZvecCollection.CreateAndOpen(CollPath("col1"), schema =>
        {
            schema.AddVector("vec", 64);
        });

        using var col2 = ZvecCollection.CreateAndOpen(CollPath("col2"), schema =>
        {
            schema.AddVector("vec", 128);
            schema.AddScalar("label", DataType.String);
        });

        col1.Flush();
        col2.Flush();
    }

    // =========================================================================
    // Metric type variations
    // =========================================================================

    [Fact]
    public void CreateAndOpen_WithL2Metric_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 64, MetricType.L2);
        });

        Assert.NotNull(col);
    }

    [Fact]
    public void CreateAndOpen_WithIPMetric_Succeeds()
    {
        using var col = ZvecCollection.CreateAndOpen(CollPath(), schema =>
        {
            schema.AddVector("vec", 64, MetricType.InnerProduct);
        });

        Assert.NotNull(col);
    }
}
