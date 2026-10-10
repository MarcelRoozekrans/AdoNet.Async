namespace System.Data.Async.Converters;

internal static class TrimmingMessages
{
    internal const string Reflection =
        "DataTable column expressions and column types are restored by reflection, which trimming can break. "
        + "Do not trim or publish this converter with NativeAOT; serialize plain DTOs with a source-generated "
        + "serializer context instead.";
}
