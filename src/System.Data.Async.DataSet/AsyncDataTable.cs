using System.Globalization;
using System.Runtime.Serialization;
using System.Xml;
using ZeroAlloc.AsyncEvents;

namespace System.Data.Async.DataSet;

public class AsyncDataTable : IDisposable
{
    private readonly DataTable _inner;
    private readonly AsyncDataRowCollection _rows;

    // Internal async event backing fields — accessed by AsyncDataRow and AsyncDataRowCollection
    internal AsyncEventHandler<DataColumnChangeEventArgs> _columnChanging = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataColumnChangeEventArgs> _columnChanged = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataRowChangeEventArgs> _rowChanging = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataRowChangeEventArgs> _rowChanged = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataRowChangeEventArgs> _rowDeleting = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataRowChangeEventArgs> _rowDeleted = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataTableClearEventArgs> _tableClearing = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataTableClearEventArgs> _tableCleared = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataTableNewRowEventArgs> _tableNewRow = new(InvokeMode.Sequential);

    public AsyncDataTable()
    {
        _inner = new DataTable();
        _rows = new AsyncDataRowCollection(_inner.Rows, this);
    }

    public AsyncDataTable(string tableName)
    {
        _inner = new DataTable(tableName);
        _rows = new AsyncDataRowCollection(_inner.Rows, this);
    }

    public AsyncDataTable(string tableName, string tableNamespace)
    {
        _inner = new DataTable(tableName, tableNamespace);
        _rows = new AsyncDataRowCollection(_inner.Rows, this);
    }

    public AsyncDataTable(DataTable inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _rows = new AsyncDataRowCollection(_inner.Rows, this);
    }

    public DataTable InnerDataTable => _inner;

    // Properties
    public string TableName { get => _inner.TableName; set => _inner.TableName = value; }
    public string Namespace { get => _inner.Namespace; set => _inner.Namespace = value; }
    public string Prefix { get => _inner.Prefix; set => _inner.Prefix = value; }
    public bool CaseSensitive { get => _inner.CaseSensitive; set => _inner.CaseSensitive = value; }
    public CultureInfo Locale { get => _inner.Locale; set => _inner.Locale = value; }
    public string DisplayExpression { get => _inner.DisplayExpression; set => _inner.DisplayExpression = value; }
    public bool HasErrors => _inner.HasErrors;
    public int MinimumCapacity { get => _inner.MinimumCapacity; set => _inner.MinimumCapacity = value; }
    public SerializationFormat RemotingFormat { get => _inner.RemotingFormat; set => _inner.RemotingFormat = value; }
    public bool IsInitialized => _inner.IsInitialized;

    // Collections
    public DataColumnCollection Columns => _inner.Columns;
    public AsyncDataRowCollection Rows => _rows;
    public ConstraintCollection Constraints => _inner.Constraints;
    public DataRelationCollection ParentRelations => _inner.ParentRelations;
    public DataRelationCollection ChildRelations => _inner.ChildRelations;
    public DataView DefaultView => _inner.DefaultView;
    public PropertyCollection ExtendedProperties => _inner.ExtendedProperties;
    public DataColumn[] PrimaryKey { get => _inner.PrimaryKey; set => _inner.PrimaryKey = value; }
    public System.Data.DataSet? DataSet => _inner.DataSet;

    // Methods
    public AsyncDataRow NewRow() => new(_inner.NewRow(), this);
    public void ImportRow(DataRow row) => _inner.ImportRow(row);
    [Obsolete("Use AcceptChangesAsync(). Calling AcceptChanges() bypasses async events.", error: true)]
    public void AcceptChanges() => throw new NotSupportedException("Use AcceptChangesAsync().");
    public void RejectChanges() => _inner.RejectChanges();
    public DataTable? GetChanges() => _inner.GetChanges();
    public DataTable? GetChanges(DataRowState rowStates) => _inner.GetChanges(rowStates);
    [Obsolete("Use ClearAsync(). Calling Clear() bypasses async events.", error: true)]
    public void Clear() => throw new NotSupportedException("Use ClearAsync().");
    public DataTable Clone() => _inner.Clone();
    public DataTable Copy() => _inner.Copy();
    public void Merge(DataTable table) => _inner.Merge(table);
    public void Merge(DataTable table, bool preserveChanges) => _inner.Merge(table, preserveChanges);

    public void Merge(DataTable table, bool preserveChanges, MissingSchemaAction missingSchemaAction)
        => _inner.Merge(table, preserveChanges, missingSchemaAction);

    public DataRow[] Select() => _inner.Select();
    public DataRow[] Select(string? filterExpression) => _inner.Select(filterExpression);
    public DataRow[] Select(string? filterExpression, string? sort) => _inner.Select(filterExpression, sort);

    public DataRow[] Select(string? filterExpression, string? sort, DataViewRowState recordStates)
        => _inner.Select(filterExpression, sort, recordStates);

    public DataRow LoadDataRow(object?[] values, bool fAcceptChanges) => _inner.LoadDataRow(values, fAcceptChanges);
    public DataRow LoadDataRow(object?[] values, LoadOption loadOption) => _inner.LoadDataRow(values, loadOption);
    public object Compute(string? expression, string? filter) => _inner.Compute(expression, filter);
    public void BeginInit() => _inner.BeginInit();
    public void EndInit() => _inner.EndInit();
    public void BeginLoadData() => _inner.BeginLoadData();
    public void EndLoadData() => _inner.EndLoadData();
    public DataRow[] GetErrors() => _inner.GetErrors();
    public void Reset() => _inner.Reset();

    // Async table-level mutations
    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        await _tableClearing.InvokeAsync(new DataTableClearEventArgs(_inner), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _inner.Clear();
        await _tableCleared.InvokeAsync(new DataTableClearEventArgs(_inner), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AcceptChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var changedRows = _inner.GetChanges(DataRowState.Modified | DataRowState.Added);
        _inner.AcceptChanges();
        if (changedRows is not null)
        {
            foreach (DataRow row in changedRows.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _rowChanged.InvokeAsync(new DataRowChangeEventArgs(row, DataRowAction.Commit), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Sync I/O
    public void Load(IDataReader reader) => _inner.Load(reader);
    public void Load(IDataReader reader, LoadOption loadOption) => _inner.Load(reader, loadOption);
    public XmlReadMode ReadXml(Stream stream) => _inner.ReadXml(stream);
    public void ReadXmlSchema(Stream stream) => _inner.ReadXmlSchema(stream);
    public void WriteXml(Stream stream) => _inner.WriteXml(stream);
    public void WriteXml(Stream stream, XmlWriteMode mode) => _inner.WriteXml(stream, mode);
    public void WriteXmlSchema(Stream stream) => _inner.WriteXmlSchema(stream);

    // Async XML I/O
    public ValueTask ReadXmlAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        _inner.ReadXml(reader);
        return default;
    }

    public async ValueTask WriteXmlAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var writer = XmlWriter.Create(stream, new XmlWriterSettings { Async = true });
        await using (writer.ConfigureAwait(false))
        {
            _inner.WriteXml(writer);
            await writer.FlushAsync().ConfigureAwait(false);
        }
    }

    public ValueTask ReadXmlSchemaAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        _inner.ReadXmlSchema(reader);
        return default;
    }

    public async ValueTask WriteXmlSchemaAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var writer = XmlWriter.Create(stream, new XmlWriterSettings { Async = true });
        await using (writer.ConfigureAwait(false))
        {
            _inner.WriteXmlSchema(writer);
            await writer.FlushAsync().ConfigureAwait(false);
        }
    }

    // Async loading from IAsyncDataReader
    public async ValueTask<int> LoadAsync(IAsyncDataReader reader, CancellationToken cancellationToken = default)
    {
        return await LoadAsync(reader, LoadOption.OverwriteChanges, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> LoadAsync(IAsyncDataReader reader, LoadOption loadOption, CancellationToken cancellationToken = default)
    {
        if (_inner.Columns.Count == 0)
        {
            var schemaTable = await reader.GetSchemaTableAsync(cancellationToken).ConfigureAwait(false);
            if (schemaTable != null)
            {
                foreach (DataRow schemaRow in schemaTable.Rows)
                {
                    var columnName = (string)schemaRow["ColumnName"];
                    var dataType = (Type)schemaRow["DataType"];
                    if (!_inner.Columns.Contains(columnName))
                    {
                        _inner.Columns.Add(columnName, dataType);
                    }
                }
            }
        }

        int count = 0;
        _inner.BeginLoadData();
        try
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                _inner.LoadDataRow(values, loadOption);
                count++;
            }
        }
        finally
        {
            _inner.EndLoadData();
        }

        return count;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }
    }

    // Implicit conversion
    public static implicit operator DataTable(AsyncDataTable asyncTable) => asyncTable._inner;

    // Sync events (preserved)
    public event DataColumnChangeEventHandler? ColumnChanged
    {
        add => _inner.ColumnChanged += value;
        remove => _inner.ColumnChanged -= value;
    }

    public event DataColumnChangeEventHandler? ColumnChanging
    {
        add => _inner.ColumnChanging += value;
        remove => _inner.ColumnChanging -= value;
    }

    public event DataRowChangeEventHandler? RowChanged
    {
        add => _inner.RowChanged += value;
        remove => _inner.RowChanged -= value;
    }

    public event DataRowChangeEventHandler? RowChanging
    {
        add => _inner.RowChanging += value;
        remove => _inner.RowChanging -= value;
    }

    public event DataRowChangeEventHandler? RowDeleted
    {
        add => _inner.RowDeleted += value;
        remove => _inner.RowDeleted -= value;
    }

    public event DataRowChangeEventHandler? RowDeleting
    {
        add => _inner.RowDeleting += value;
        remove => _inner.RowDeleting -= value;
    }

    public event DataTableClearEventHandler? TableCleared
    {
        add => _inner.TableCleared += value;
        remove => _inner.TableCleared -= value;
    }

    public event DataTableClearEventHandler? TableClearing
    {
        add => _inner.TableClearing += value;
        remove => _inner.TableClearing -= value;
    }

    public event DataTableNewRowEventHandler? TableNewRow
    {
        add => _inner.TableNewRow += value;
        remove => _inner.TableNewRow -= value;
    }

    // Async events — AsyncEvent<TArgs> returns ValueTask, not void; MA0046 suppressed intentionally
#pragma warning disable MA0046
    public event AsyncEvent<DataColumnChangeEventArgs> ColumnChangingAsync
    {
        add => _columnChanging.Register(value);
        remove => _columnChanging.Unregister(value);
    }

    public event AsyncEvent<DataColumnChangeEventArgs> ColumnChangedAsync
    {
        add => _columnChanged.Register(value);
        remove => _columnChanged.Unregister(value);
    }

    public event AsyncEvent<DataRowChangeEventArgs> RowChangingAsync
    {
        add => _rowChanging.Register(value);
        remove => _rowChanging.Unregister(value);
    }

    public event AsyncEvent<DataRowChangeEventArgs> RowChangedAsync
    {
        add => _rowChanged.Register(value);
        remove => _rowChanged.Unregister(value);
    }

    public event AsyncEvent<DataRowChangeEventArgs> RowDeletingAsync
    {
        add => _rowDeleting.Register(value);
        remove => _rowDeleting.Unregister(value);
    }

    public event AsyncEvent<DataRowChangeEventArgs> RowDeletedAsync
    {
        add => _rowDeleted.Register(value);
        remove => _rowDeleted.Unregister(value);
    }

    public event AsyncEvent<DataTableClearEventArgs> TableClearingAsync
    {
        add => _tableClearing.Register(value);
        remove => _tableClearing.Unregister(value);
    }

    public event AsyncEvent<DataTableClearEventArgs> TableClearedAsync
    {
        add => _tableCleared.Register(value);
        remove => _tableCleared.Unregister(value);
    }

    public event AsyncEvent<DataTableNewRowEventArgs> TableNewRowAsync
    {
        add => _tableNewRow.Register(value);
        remove => _tableNewRow.Unregister(value);
    }
#pragma warning restore MA0046
}
