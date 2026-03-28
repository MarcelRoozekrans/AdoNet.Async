namespace System.Data.Async.DataSet.Generator.Model;

internal enum NullValueBehaviorKind
{
    Throw,
    ReturnNull,
    ReturnEmpty,
    ReplacementValue,
}

internal sealed record NullValueBehavior(NullValueBehaviorKind Kind, string? ReplacementValue = null);
