namespace System.Data.Async;

public interface IAsyncDbProviderFactory
{
    IAsyncDbConnection CreateConnection();
    IAsyncDbCommand CreateCommand();
    IDbDataParameter CreateParameter();
}
