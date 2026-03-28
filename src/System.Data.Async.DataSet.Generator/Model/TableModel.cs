using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record TableModel(
    string Name,
    string? TypedName,
    string? TypedPlural,
    ImmutableArray<ColumnModel> Columns,
    ImmutableArray<string> PrimaryKeyColumnNames,
    ImmutableArray<UniqueConstraintModel> UniqueConstraints);
