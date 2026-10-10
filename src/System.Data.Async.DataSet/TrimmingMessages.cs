namespace System.Data.Async.DataSet;

internal static class TrimmingMessages
{
    internal const string Xml =
        "DataSet and DataTable XML serialization reads and writes members of the contained value types by "
        + "reflection and can generate code at run time, which trimming and NativeAOT can break. "
        + "Use the JSON converters or plain DTOs with a source-generated serializer context instead.";

    internal const string Expressions =
        "DataTable filter, sort, compute and display expressions are evaluated by reflection over the column "
        + "types, which trimming can break. Filter and aggregate with LINQ over the rows instead.";

    internal const string Load =
        "Creating columns from the data reader's runtime column types and loading rows into them uses "
        + "reflection over those types, which trimming can break. Declare the columns up front with "
        + "statically known types instead.";
}
