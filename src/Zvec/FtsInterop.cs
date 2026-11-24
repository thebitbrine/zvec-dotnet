using Zvec.Native;

namespace Zvec;

/// <summary>
/// Shared native-interop helper for FTS index parameters.
/// </summary>
internal static class FtsInterop
{
    /// <summary>
    /// Apply tokenizer/filter/extra params to an FTS index params handle.
    /// Nulls keep the native defaults. No-op when everything is null.
    /// </summary>
    internal static void ApplyFtsParams(nint indexParamsHandle,
        string? tokenizerName, IReadOnlyList<string>? filters, string? extraParams)
    {
        if (tokenizerName is null && (filters is null || filters.Count == 0) && extraParams is null)
            return;

        nint filterArray = 0;
        try
        {
            if (filters is { Count: > 0 })
            {
                filterArray = NativeMethods.zvec_string_array_create((nuint)filters.Count);
                if (filterArray == 0)
                    throw new ZvecException(ZvecErrorCode.InternalError, "Failed to create filter string array");

                for (int i = 0; i < filters.Count; i++)
                    NativeMethods.zvec_string_array_add(filterArray, (nuint)i, filters[i]);
            }

            ZvecError.ThrowIfFailed(
                NativeMethods.zvec_index_params_set_fts_params(
                    indexParamsHandle, tokenizerName, filterArray, extraParams));
        }
        finally
        {
            if (filterArray != 0)
                NativeMethods.zvec_string_array_destroy(filterArray);
        }
    }
}
