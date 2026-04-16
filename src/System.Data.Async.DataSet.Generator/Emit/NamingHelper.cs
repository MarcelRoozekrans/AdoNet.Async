using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Emit;

internal static class NamingHelper
{
    public static string RowClassName(string dataSetName, string tableName, string? typedName)
        => $"Async{dataSetName}{typedName ?? tableName}Row";

    public static string TableClassName(string dataSetName, string tableName, string? typedPlural)
        => $"Async{dataSetName}{typedPlural ?? tableName}DataTable";

    public static string DataSetClassName(string dsName)
        => $"Async{dsName}";

    public static string EventArgsClassName(string dataSetName, string tableName, string? typedName)
        => $"Async{dataSetName}{typedName ?? tableName}RowChangeEvent";

    public static string FindByMethodName(ImmutableArray<string> pkColumns)
        => "FindBy" + string.Join("", pkColumns);

    public static string SetterMethodName(string columnName)
        => $"Set{columnName}Async";

    public static string IsNullMethodName(string columnName)
        => $"Is{columnName}Null";

    public static string SetNullMethodName(string columnName)
        => $"Set{columnName}NullAsync";

    public static string AddRowMethodName(string tableName, string? typedName)
        => $"Add{typedName ?? tableName}RowAsync";

    public static string RemoveRowMethodName(string tableName, string? typedName)
        => $"Remove{typedName ?? tableName}RowAsync";

    public static string NewRowMethodName(string tableName, string? typedName)
        => $"New{typedName ?? tableName}Row";

    public static string GetChildRowsMethodName(string childTableName, string? typedChildren)
        => typedChildren ?? $"Get{childTableName}Rows";

    public static string ParentRowPropertyName(string parentTableName, string? typedParent)
        => typedParent != null ? $"{typedParent}Row" : $"{parentTableName}Row";

    public static string SetParentRowMethodName(string parentTableName, string? typedParent)
        => $"Set{ParentRowPropertyName(parentTableName, typedParent)}Async";

    /// <summary>
    /// Maps CLR type names to C# keywords for generated code.
    /// </summary>
    public static string ClrTypeToKeyword(string clrType) => clrType switch
    {
        "System.String" => "string",
        "System.Int32" => "int",
        "System.Int64" => "long",
        "System.Int16" => "short",
        "System.Byte" => "byte",
        "System.SByte" => "sbyte",
        "System.UInt16" => "ushort",
        "System.UInt32" => "uint",
        "System.UInt64" => "ulong",
        "System.Boolean" => "bool",
        "System.Decimal" => "decimal",
        "System.Double" => "double",
        "System.Single" => "float",
        "System.DateTime" => "global::System.DateTime",
        "System.Guid" => "global::System.Guid",
        "System.TimeSpan" => "global::System.TimeSpan",
        "System.Byte[]" => "byte[]",
        _ => $"global::{clrType}"
    };
}
