namespace Zvec.Native;

/// <summary>
/// Maps ZVEC_INDEX_TYPE_* constants from c_api.h.
/// </summary>
public enum IndexType : uint
{
    Undefined = 0,
    Hnsw      = 1,
    Ivf       = 2,
    Flat      = 3,
    Invert    = 10,
    Fts       = 11,
}
