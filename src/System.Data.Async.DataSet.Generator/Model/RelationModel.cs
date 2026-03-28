using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record RelationModel(
    string Name,
    string ParentTableName,
    ImmutableArray<string> ParentColumnNames,
    string ChildTableName,
    ImmutableArray<string> ChildColumnNames,
    bool Nested,
    bool ConstraintOnly,
    string? TypedParent,
    string? TypedChildren);
