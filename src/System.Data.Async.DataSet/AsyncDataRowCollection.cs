using System.Collections;

namespace System.Data.Async.DataSet;

public class AsyncDataRowCollection : IEnumerable<AsyncDataRow>
{
    private readonly DataRowCollection _inner;
    private readonly AsyncDataTable _table;

    protected DataRowCollection InnerCollection => _inner;
    protected AsyncDataTable Table => _table;

    protected internal AsyncDataRowCollection(DataRowCollection inner, AsyncDataTable table)
    {
        _inner = inner;
        _table = table;
    }

    public int Count => _inner.Count;
    public bool Contains(object key) => _inner.Contains(key);

    public AsyncDataRow this[int index] => new(_inner[index], _table);

    public async ValueTask AddAsync(AsyncDataRow row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        _inner.Add(row.InnerDataRow);
        await _table._tableNewRow.InvokeAsync(new DataTableNewRowEventArgs(row.InnerDataRow), cancellationToken).ConfigureAwait(false);
        await _table._rowChanged.InvokeAsync(new DataRowChangeEventArgs(row.InnerDataRow, DataRowAction.Add), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AddAsync(object?[] values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length > _table.InnerDataTable.Columns.Count)
            throw new ArgumentException(
                $"Array length ({values.Length}) exceeds column count ({_table.InnerDataTable.Columns.Count}).",
                nameof(values));
        var row = _table.InnerDataTable.NewRow();
        for (int i = 0; i < values.Length; i++)
        {
            row[i] = values[i] ?? DBNull.Value;
        }
        _inner.Add(row);
        await _table._tableNewRow.InvokeAsync(new DataTableNewRowEventArgs(row), cancellationToken).ConfigureAwait(false);
        await _table._rowChanged.InvokeAsync(new DataRowChangeEventArgs(row, DataRowAction.Add), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RemoveAsync(AsyncDataRow row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        var args = new DataRowChangeEventArgs(row.InnerDataRow, DataRowAction.Delete);
        await _table._rowDeleting.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
        _inner.Remove(row.InnerDataRow);
        await _table._rowDeleted.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RemoveAtAsync(int index, CancellationToken cancellationToken = default)
    {
        var innerRow = _inner[index];
        var args = new DataRowChangeEventArgs(innerRow, DataRowAction.Delete);
        await _table._rowDeleting.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
        _inner.RemoveAt(index);
        await _table._rowDeleted.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
    }

#pragma warning disable HLQ006 // GetEnumerator returns reference type — async-row wrapping requires compiler-generated iterator
    public IEnumerator<AsyncDataRow> GetEnumerator()
    {
        for (int i = 0; i < _inner.Count; i++)
        {
            yield return new AsyncDataRow(_inner[i], _table);
        }
    }
#pragma warning restore HLQ006

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
