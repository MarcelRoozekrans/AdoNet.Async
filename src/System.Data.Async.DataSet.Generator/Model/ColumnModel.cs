namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record ColumnModel(
    string Name,
    string ClrTypeName,
    bool AllowDBNull,
    bool ReadOnly,
    string? Expression,
    bool AutoIncrement,
    long AutoIncrementSeed,
    long AutoIncrementStep,
    string? DefaultValue,
    string? Caption,
    int? Ordinal,
    int? MaxLength,
    bool IsHidden,
    NullValueBehavior NullValueBehavior);
