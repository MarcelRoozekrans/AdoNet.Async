using System.Collections;

namespace System.Data.Async.DataSet;

public class AsyncDataRowCollection<TRow> : AsyncDataRowCollection, IEnumerable<TRow>
    where TRow : AsyncDataRow
{
    private readonly Func<DataRow, AsyncDataTable, TRow> _rowFactory;

    public AsyncDataRowCollection(
        DataRowCollection inner,
        AsyncDataTable table,
        Func<DataRow, AsyncDataTable, TRow> rowFactory)
        : base(inner, table)
    {
        _rowFactory = rowFactory;
    }

    public new TRow this[int index] => _rowFactory(InnerCollection[index], Table);

#pragma warning disable HLQ006
    public new IEnumerator<TRow> GetEnumerator()
    {
        for (int i = 0; i < InnerCollection.Count; i++)
        {
            yield return _rowFactory(InnerCollection[i], Table);
        }
    }
#pragma warning restore HLQ006

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
