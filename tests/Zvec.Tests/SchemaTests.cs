namespace Zvec.Tests;

[Collection("Zvec")]
public class SchemaTests
{
    // =========================================================================
    // Raw handle tests -- verify native functions work at the P/Invoke level
    // =========================================================================

    [Fact]
    public void FieldSchemaCreate_VectorFp32_ReturnsNonZero()
    {
        nint handle = NativeMethods.zvec_field_schema_create("embedding", (uint)DataType.VectorFp32, false, 128);
        Assert.NotEqual(0, handle);
        NativeMethods.zvec_field_schema_destroy(handle);
    }

    [Fact]
    public void FieldSchemaCreate_ScalarString_ReturnsNonZero()
    {
        nint handle = NativeMethods.zvec_field_schema_create("title", (uint)DataType.String, false, 0);
        Assert.NotEqual(0, handle);
        NativeMethods.zvec_field_schema_destroy(handle);
    }

    [Fact]
    public void FieldSchemaCreate_ScalarInt32_ReturnsNonZero()
    {
        nint handle = NativeMethods.zvec_field_schema_create("count", (uint)DataType.Int32, false, 0);
        Assert.NotEqual(0, handle);
        NativeMethods.zvec_field_schema_destroy(handle);
    }

    [Fact]
    public void FieldSchemaCreate_NullableField_ReturnsNonZero()
    {
        nint handle = NativeMethods.zvec_field_schema_create("optional", (uint)DataType.String, true, 0);
        Assert.NotEqual(0, handle);
        NativeMethods.zvec_field_schema_destroy(handle);
    }

    [Fact]
    public void FieldSchemaCreate_VariousDimensions_Succeeds()
    {
        // Small dimension
        nint h1 = NativeMethods.zvec_field_schema_create("v1", (uint)DataType.VectorFp32, false, 1);
        Assert.NotEqual(0, h1);
        NativeMethods.zvec_field_schema_destroy(h1);

        // Large dimension
        nint h2 = NativeMethods.zvec_field_schema_create("v2", (uint)DataType.VectorFp32, false, 2048);
        Assert.NotEqual(0, h2);
        NativeMethods.zvec_field_schema_destroy(h2);
    }

    [Fact]
    public void IndexParamsCreate_Hnsw_ReturnsNonZero()
    {
        nint handle = NativeMethods.zvec_index_params_create((uint)IndexType.Hnsw);
        Assert.NotEqual(0, handle);
        NativeMethods.zvec_index_params_destroy(handle);
    }

    [Fact]
    public void IndexParamsCreate_Flat_ReturnsNonZero()
    {
        nint handle = NativeMethods.zvec_index_params_create((uint)IndexType.Flat);
        Assert.NotEqual(0, handle);
        NativeMethods.zvec_index_params_destroy(handle);
    }

    [Fact]
    public void IndexParamsCreate_Ivf_ReturnsNonZero()
    {
        nint handle = NativeMethods.zvec_index_params_create((uint)IndexType.Ivf);
        Assert.NotEqual(0, handle);
        NativeMethods.zvec_index_params_destroy(handle);
    }

    [Fact]
    public void IndexParamsSetMetricType_Cosine_Succeeds()
    {
        nint handle = NativeMethods.zvec_index_params_create((uint)IndexType.Hnsw);
        try
        {
            uint result = NativeMethods.zvec_index_params_set_metric_type(handle, (uint)MetricType.Cosine);
            Assert.Equal(0u, result);
        }
        finally
        {
            NativeMethods.zvec_index_params_destroy(handle);
        }
    }

    [Fact]
    public void IndexParamsSetMetricType_L2_Succeeds()
    {
        nint handle = NativeMethods.zvec_index_params_create((uint)IndexType.Hnsw);
        try
        {
            uint result = NativeMethods.zvec_index_params_set_metric_type(handle, (uint)MetricType.L2);
            Assert.Equal(0u, result);
        }
        finally
        {
            NativeMethods.zvec_index_params_destroy(handle);
        }
    }

    [Fact]
    public void IndexParamsSetMetricType_InnerProduct_Succeeds()
    {
        nint handle = NativeMethods.zvec_index_params_create((uint)IndexType.Hnsw);
        try
        {
            uint result = NativeMethods.zvec_index_params_set_metric_type(handle, (uint)MetricType.InnerProduct);
            Assert.Equal(0u, result);
        }
        finally
        {
            NativeMethods.zvec_index_params_destroy(handle);
        }
    }

    [Fact]
    public void IndexParamsSetHnswParams_Succeeds()
    {
        nint handle = NativeMethods.zvec_index_params_create((uint)IndexType.Hnsw);
        try
        {
            uint result = NativeMethods.zvec_index_params_set_hnsw_params(handle, 16, 200);
            Assert.Equal(0u, result);
        }
        finally
        {
            NativeMethods.zvec_index_params_destroy(handle);
        }
    }

    [Fact]
    public void FieldSchemaSetIndexParams_Succeeds()
    {
        nint fieldHandle = NativeMethods.zvec_field_schema_create("vec", (uint)DataType.VectorFp32, false, 128);
        nint paramsHandle = NativeMethods.zvec_index_params_create((uint)IndexType.Hnsw);

        try
        {
            NativeMethods.zvec_index_params_set_metric_type(paramsHandle, (uint)MetricType.Cosine);
            uint result = NativeMethods.zvec_field_schema_set_index_params(fieldHandle, paramsHandle);
            Assert.Equal(0u, result);
        }
        finally
        {
            // Both are still ours to destroy (set_index_params copies)
            NativeMethods.zvec_index_params_destroy(paramsHandle);
            NativeMethods.zvec_field_schema_destroy(fieldHandle);
        }
    }

    [Fact]
    public void CollectionSchemaCreate_ReturnsNonZero()
    {
        nint handle = NativeMethods.zvec_collection_schema_create("test_collection");
        Assert.NotEqual(0, handle);
        NativeMethods.zvec_collection_schema_destroy(handle);
    }

    [Fact]
    public void CollectionSchemaAddField_VectorField_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("test");
        nint fieldHandle = NativeMethods.zvec_field_schema_create("vec", (uint)DataType.VectorFp32, false, 64);

        try
        {
            uint result = NativeMethods.zvec_collection_schema_add_field(schemaHandle, fieldHandle);
            Assert.Equal(0u, result);
        }
        finally
        {
            // add_field clones, both handles are still ours
            NativeMethods.zvec_field_schema_destroy(fieldHandle);
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    [Fact]
    public void CollectionSchemaAddField_MultipleFields_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("multi");

        nint vecField = NativeMethods.zvec_field_schema_create("vec", (uint)DataType.VectorFp32, false, 128);
        nint strField = NativeMethods.zvec_field_schema_create("title", (uint)DataType.String, false, 0);
        nint intField = NativeMethods.zvec_field_schema_create("count", (uint)DataType.Int32, true, 0);

        try
        {
            Assert.Equal(0u, NativeMethods.zvec_collection_schema_add_field(schemaHandle, vecField));
            Assert.Equal(0u, NativeMethods.zvec_collection_schema_add_field(schemaHandle, strField));
            Assert.Equal(0u, NativeMethods.zvec_collection_schema_add_field(schemaHandle, intField));
        }
        finally
        {
            NativeMethods.zvec_field_schema_destroy(intField);
            NativeMethods.zvec_field_schema_destroy(strField);
            NativeMethods.zvec_field_schema_destroy(vecField);
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    // =========================================================================
    // ZvecSchema builder tests -- verify the public API wrapper
    // =========================================================================

    [Fact]
    public void ZvecSchema_AddVector_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("builder_test");
        try
        {
            var builder = new ZvecSchema(schemaHandle);
            builder.AddVector("embedding", 128, MetricType.Cosine, IndexType.Hnsw);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    [Fact]
    public void ZvecSchema_AddScalar_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("builder_test");
        try
        {
            var builder = new ZvecSchema(schemaHandle);
            builder.AddScalar("title", DataType.String);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    [Fact]
    public void ZvecSchema_AddMultipleFields_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("builder_multi");
        try
        {
            var builder = new ZvecSchema(schemaHandle);
            builder.AddVector("embedding", 1024, MetricType.Cosine);
            builder.AddScalar("title", DataType.String);
            builder.AddScalar("year", DataType.Int32);
            builder.AddScalar("score", DataType.Float, nullable: true);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    [Fact]
    public void ZvecSchema_AddVector_WithL2Metric_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("l2_test");
        try
        {
            var builder = new ZvecSchema(schemaHandle);
            builder.AddVector("vec", 64, MetricType.L2, IndexType.Flat);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    [Fact]
    public void ZvecSchema_AddVector_WithInnerProduct_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("ip_test");
        try
        {
            var builder = new ZvecSchema(schemaHandle);
            builder.AddVector("vec", 256, MetricType.InnerProduct, IndexType.Hnsw);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    [Fact]
    public void ZvecSchema_AddScalar_AllBasicTypes_Succeed()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("types_test");
        try
        {
            var builder = new ZvecSchema(schemaHandle);
            builder.AddScalar("s", DataType.String);
            builder.AddScalar("i32", DataType.Int32);
            builder.AddScalar("i64", DataType.Int64);
            builder.AddScalar("f", DataType.Float);
            builder.AddScalar("d", DataType.Double);
            builder.AddScalar("b", DataType.Bool);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    [Fact]
    public void ZvecSchema_AddVector_SmallDimension_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("dim1_test");
        try
        {
            var builder = new ZvecSchema(schemaHandle);
            builder.AddVector("vec", 1);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    [Fact]
    public void ZvecSchema_AddVector_LargeDimension_Succeeds()
    {
        nint schemaHandle = NativeMethods.zvec_collection_schema_create("dim2048_test");
        try
        {
            var builder = new ZvecSchema(schemaHandle);
            builder.AddVector("vec", 2048);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaHandle);
        }
    }

    // =========================================================================
    // Handle cleanup -- verify no crashes on GC
    // =========================================================================

    [Fact]
    public void HandleCleanup_AfterGC_NoCrash()
    {
        // Create and abandon handles, force GC -- should not crash
        for (int i = 0; i < 10; i++)
        {
            nint h = NativeMethods.zvec_field_schema_create($"gc_test_{i}", (uint)DataType.String, false, 0);
            NativeMethods.zvec_field_schema_destroy(h);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Fact]
    public void CollectionOptionsCreate_ReturnsNonZero()
    {
        nint handle = NativeMethods.zvec_collection_options_create();
        Assert.NotEqual(0, handle);
        NativeMethods.zvec_collection_options_destroy(handle);
    }
}
