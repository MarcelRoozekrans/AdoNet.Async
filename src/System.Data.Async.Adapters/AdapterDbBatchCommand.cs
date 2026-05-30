using System.Data.Common;

namespace System.Data.Async.Adapters;

public sealed class AdapterDbBatchCommand : IAsyncDbBatchCommand
{
    private readonly DbBatchCommand _inner;

    public AdapterDbBatchCommand(DbBatchCommand inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    internal DbBatchCommand InnerCommand => _inner;
    public static explicit operator DbBatchCommand(AdapterDbBatchCommand command) => command._inner;

    public string CommandText
    {
        get => _inner.CommandText;
        set => _inner.CommandText = value;
    }

    public CommandType CommandType
    {
        get => _inner.CommandType;
        set => _inner.CommandType = value;
    }

    public IDataParameterCollection Parameters => _inner.Parameters;
    public int RecordsAffected => _inner.RecordsAffected;

    public IDbDataParameter CreateParameter() => _inner.CreateParameter();
}
