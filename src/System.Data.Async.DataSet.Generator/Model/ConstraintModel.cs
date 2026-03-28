using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record UniqueConstraintModel(
    string Name,
    string TableName,
    ImmutableArray<string> ColumnNames,
    bool IsPrimaryKey);

internal sealed record ForeignKeyConstraintModel(
    string Name,
    string ParentTableName,
    ImmutableArray<string> ParentColumnNames,
    string ChildTableName,
    ImmutableArray<string> ChildColumnNames,
    string UpdateRule,
    string DeleteRule,
    string AcceptRejectRule);
