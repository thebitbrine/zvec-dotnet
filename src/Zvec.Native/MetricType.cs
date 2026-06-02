namespace Zvec.Native;

/// <summary>
/// Maps ZVEC_METRIC_TYPE_* constants from c_api.h.
/// </summary>
public enum MetricType : uint
{
    Undefined    = 0,
    L2           = 1,
    InnerProduct = 2,
    Cosine       = 3,
    MipsL2       = 4,
}
