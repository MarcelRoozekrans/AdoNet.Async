using System.Collections;

namespace System.Data.Async.DataSet;

public class AsyncDataTableCollection : IEnumerable<AsyncDataTable>
{
    private readonly DataTableCollection _inner;
    private readonly AsyncDataSet _parent;

    internal AsyncDataTableCollection(DataTableCollection inner, AsyncDataSet parent)
    {
        _inner = inner;
        _parent = parent;
    }

    public int Count => _inner.Count;

    public AsyncDataTable this[string name]
    {
        get
        {
            var dt = _inner[name] ?? throw new ArgumentException($"Table '{name}' not found.", nameof(name));
            return _parent.GetOrCreateTable(dt);
        }
    }

    public AsyncDataTable this[int index] => _parent.GetOrCreateTable(_inner[index]);

    public bool Contains(string name) => _inner.Contains(name);

    public void Add(AsyncDataTable table) => _inner.Add(table.InnerDataTable);
    public void Add(DataTable table) => _inner.Add(table);
    public void Remove(AsyncDataTable table) => _inner.Remove(table.InnerDataTable);
    public void Remove(DataTable table) => _inner.Remove(table);
    public void Remove(string name) => _inner.Remove(name);

#pragma warning disable HLQ006
    public IEnumerator<AsyncDataTable> GetEnumerator()
    {
        for (int i = 0; i < _inner.Count; i++)
            yield return _parent.GetOrCreateTable(_inner[i]);
    }
#pragma warning restore HLQ006

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
