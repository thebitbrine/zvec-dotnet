namespace Zvec.Tests;

public class EnumTests
{
    // =========================================================================
    // DataType -- verify values match ZVEC_DATA_TYPE_* from c_api.h
    // =========================================================================

    [Theory]
    [InlineData(DataType.Undefined, 0u)]
    [InlineData(DataType.Binary, 1u)]
    [InlineData(DataType.String, 2u)]
    [InlineData(DataType.Bool, 3u)]
    [InlineData(DataType.Int32, 4u)]
    [InlineData(DataType.Int64, 5u)]
    [InlineData(DataType.UInt32, 6u)]
    [InlineData(DataType.UInt64, 7u)]
    [InlineData(DataType.Float, 8u)]
    [InlineData(DataType.Double, 9u)]
    [InlineData(DataType.VectorBinary32, 20u)]
    [InlineData(DataType.VectorBinary64, 21u)]
    [InlineData(DataType.VectorFp16, 22u)]
    [InlineData(DataType.VectorFp32, 23u)]
    [InlineData(DataType.VectorFp64, 24u)]
    [InlineData(DataType.VectorInt4, 25u)]
    [InlineData(DataType.VectorInt8, 26u)]
    [InlineData(DataType.VectorInt16, 27u)]
    [InlineData(DataType.SparseVectorFp16, 30u)]
    [InlineData(DataType.SparseVectorFp32, 31u)]
    [InlineData(DataType.ArrayBinary, 40u)]
    [InlineData(DataType.ArrayString, 41u)]
    [InlineData(DataType.ArrayBool, 42u)]
    [InlineData(DataType.ArrayInt32, 43u)]
    [InlineData(DataType.ArrayInt64, 44u)]
    [InlineData(DataType.ArrayUInt32, 45u)]
    [InlineData(DataType.ArrayUInt64, 46u)]
    [InlineData(DataType.ArrayFloat, 47u)]
    [InlineData(DataType.ArrayDouble, 48u)]
    public void DataType_HasCorrectValue(DataType dt, uint expected)
    {
        Assert.Equal(expected, (uint)dt);
    }

    // =========================================================================
    // MetricType -- verify values match ZVEC_METRIC_TYPE_* from c_api.h
    // =========================================================================

    [Theory]
    [InlineData(MetricType.Undefined, 0u)]
    [InlineData(MetricType.L2, 1u)]
    [InlineData(MetricType.InnerProduct, 2u)]
    [InlineData(MetricType.Cosine, 3u)]
    [InlineData(MetricType.MipsL2, 4u)]
    public void MetricType_HasCorrectValue(MetricType mt, uint expected)
    {
        Assert.Equal(expected, (uint)mt);
    }

    // =========================================================================
    // IndexType -- verify values match ZVEC_INDEX_TYPE_* from c_api.h
    // =========================================================================

    [Theory]
    [InlineData(IndexType.Undefined, 0u)]
    [InlineData(IndexType.Hnsw, 1u)]
    [InlineData(IndexType.Ivf, 2u)]
    [InlineData(IndexType.Flat, 3u)]
    [InlineData(IndexType.Invert, 10u)]
    [InlineData(IndexType.Fts, 11u)]
    public void IndexType_HasCorrectValue(IndexType it, uint expected)
    {
        Assert.Equal(expected, (uint)it);
    }

    // =========================================================================
    // ZvecErrorCode -- verify values match zvec_error_code_t from c_api.h
    // =========================================================================

    [Theory]
    [InlineData(ZvecErrorCode.Ok, 0u)]
    [InlineData(ZvecErrorCode.NotFound, 1u)]
    [InlineData(ZvecErrorCode.AlreadyExists, 2u)]
    [InlineData(ZvecErrorCode.InvalidArgument, 3u)]
    [InlineData(ZvecErrorCode.PermissionDenied, 4u)]
    [InlineData(ZvecErrorCode.FailedPrecondition, 5u)]
    [InlineData(ZvecErrorCode.ResourceExhausted, 6u)]
    [InlineData(ZvecErrorCode.Unavailable, 7u)]
    [InlineData(ZvecErrorCode.InternalError, 8u)]
    [InlineData(ZvecErrorCode.NotSupported, 9u)]
    [InlineData(ZvecErrorCode.Unknown, 10u)]
    public void ZvecErrorCode_HasCorrectValue(ZvecErrorCode ec, uint expected)
    {
        Assert.Equal(expected, (uint)ec);
    }

    // =========================================================================
    // Verify enums are backed by uint (4 bytes, matching C uint32_t)
    // =========================================================================

    [Fact]
    public void DataType_IsUint32()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(DataType)));
    }

    [Fact]
    public void MetricType_IsUint32()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(MetricType)));
    }

    [Fact]
    public void IndexType_IsUint32()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(IndexType)));
    }

    [Fact]
    public void ZvecErrorCode_IsUint32()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(ZvecErrorCode)));
    }
}
