using ZeroAlloc.AsyncEvents;

namespace System.Data.Async.DataSet;

public sealed class AsyncDataRow
{
    private readonly DataRow _inner;
    private readonly AsyncDataTable _table;

    internal AsyncDataRow(DataRow inner, AsyncDataTable table)
    {
        _inner = inner;
        _table = table;
    }

    public DataRow InnerDataRow => _inner;
    public DataRowState RowState => _inner.RowState;
    public bool HasErrors => _inner.HasErrors;
    public string RowError => _inner.RowError;
    public DataTable Table => _inner.Table;
    public bool HasVersion(DataRowVersion version) => _inner.HasVersion(version);

    // Getter-only indexers
    public object this[string columnName] => _inner[columnName];
    public object this[int columnIndex] => _inner[columnIndex];
    public object this[DataColumn column] => _inner[column];
    public object this[string columnName, DataRowVersion version] => _inner[columnName, version];
    public object this[int columnIndex, DataRowVersion version] => _inner[columnIndex, version];
    public object this[DataColumn column, DataRowVersion version] => _inner[column, version];

    public ValueTask SetValueAsync(string columnName, object? value, CancellationToken cancellationToken = default)
    {
        var column = _inner.Table.Columns[columnName]
            ?? throw new ArgumentException($"Column '{columnName}' not found.", nameof(columnName));
        return SetValueCoreAsync(column, value, cancellationToken);
    }

    public ValueTask SetValueAsync(int columnIndex, object? value, CancellationToken cancellationToken = default)
    {
        var column = _inner.Table.Columns[columnIndex];
        return SetValueCoreAsync(column, value, cancellationToken);
    }

    public ValueTask SetValueAsync(DataColumn column, object? value, CancellationToken cancellationToken = default)
        => SetValueCoreAsync(column, value, cancellationToken);

    private async ValueTask SetValueCoreAsync(DataColumn column, object? value, CancellationToken cancellationToken)
    {
        var colArgs = new DataColumnChangeEventArgs(_inner, column, value);

        await _table._columnChanging.InvokeAsync(colArgs, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _inner[column] = value ?? DBNull.Value;
        await _table._columnChanged.InvokeAsync(colArgs, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(CancellationToken cancellationToken = default)
    {
        var args = new DataRowChangeEventArgs(_inner, DataRowAction.Delete);
        await _table._rowDeleting.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _inner.Delete();
        await _table._rowDeleted.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask BeginEditAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inner.BeginEdit();
        return ValueTask.CompletedTask;
    }

    public async ValueTask EndEditAsync(CancellationToken cancellationToken = default)
    {
        var preArgs = new DataRowChangeEventArgs(_inner, DataRowAction.Change);
        var postArgs = new DataRowChangeEventArgs(_inner, DataRowAction.Commit);
        await _table._rowChanging.InvokeAsync(preArgs, cancellationToken).ConfigureAwait(false);
        _inner.EndEdit();
        await _table._rowChanged.InvokeAsync(postArgs, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask CancelEditAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inner.CancelEdit();
        return ValueTask.CompletedTask;
    }

    public ValueTask AcceptChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inner.AcceptChanges();
        return ValueTask.CompletedTask;
    }

    public ValueTask RejectChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inner.RejectChanges();
        return ValueTask.CompletedTask;
    }
}
