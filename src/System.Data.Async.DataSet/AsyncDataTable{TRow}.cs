namespace System.Data.Async.DataSet;

public abstract class AsyncDataTable<TRow> : AsyncDataTable
    where TRow : AsyncDataRow
{
    private AsyncDataRowCollection<TRow>? _typedRows;

    protected AsyncDataTable(string tableName) : base(tableName) { }
    protected AsyncDataTable(string tableName, string tableNamespace) : base(tableName, tableNamespace) { }
    protected AsyncDataTable(DataTable inner) : base(inner) { }

    protected abstract TRow WrapRow(DataRow innerRow);
    protected override AsyncDataRow CreateRow(DataRow inner) => WrapRow(inner);

    public new AsyncDataRowCollection<TRow> Rows =>
        _typedRows ??= new AsyncDataRowCollection<TRow>(InnerDataTable.Rows, this, (inner, _) => WrapRow(inner));

    public new TRow NewRow()
    {
        var innerRow = InnerDataTable.NewRow();
        return WrapRow(innerRow);
    }

    public TRow this[int index] => Rows[index];
}
