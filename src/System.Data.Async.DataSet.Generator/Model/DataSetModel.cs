using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record DataSetModel(
    string Name,
    string? Namespace,
    string? Locale,
    bool CaseSensitive,
    bool EnforceConstraints,
    ImmutableArray<TableModel> Tables,
    ImmutableArray<RelationModel> Relations,
    ImmutableArray<ForeignKeyConstraintModel> ForeignKeyConstraints);
