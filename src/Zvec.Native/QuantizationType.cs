namespace Zvec.Native;

/// <summary>
/// Maps ZVEC_QUANTIZE_TYPE_* constants from c_api.h.
/// </summary>
public enum QuantizationType : uint
{
    Undefined = 0,
    Fp16      = 1,
    Int8      = 2,
    Int4      = 3,
}
