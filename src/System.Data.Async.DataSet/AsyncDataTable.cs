using System.Globalization;
using System.Runtime.Serialization;

namespace System.Data.Async.DataSet;

public class AsyncDataTable : IDisposable
{
    private readonly DataTable _inner;

    public AsyncDataTable() => _inner = new DataTable();
    public AsyncDataTable(string tableName) => _inner = new DataTable(tableName);
    public AsyncDataTable(string tableName, string tableNamespace) => _inner = new DataTable(tableName, tableNamespace);
    internal AsyncDataTable(DataTable inner) => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    internal DataTable InnerDataTable => _inner;

    // Properties - all delegate to _inner
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

    // Collections - expose inner's collections directly
    public DataColumnCollection Columns => _inner.Columns;
    public DataRowCollection Rows => _inner.Rows;
    public ConstraintCollection Constraints => _inner.Constraints;
    public DataRelationCollection ParentRelations => _inner.ParentRelations;
    public DataRelationCollection ChildRelations => _inner.ChildRelations;
    public DataView DefaultView => _inner.DefaultView;
    public PropertyCollection ExtendedProperties => _inner.ExtendedProperties;
    public DataColumn[] PrimaryKey { get => _inner.PrimaryKey; set => _inner.PrimaryKey = value; }
    public System.Data.DataSet? DataSet => _inner.DataSet;

    // Methods - all delegate to _inner
    public DataRow NewRow() => _inner.NewRow();
    public void ImportRow(DataRow row) => _inner.ImportRow(row);
    public void AcceptChanges() => _inner.AcceptChanges();
    public void RejectChanges() => _inner.RejectChanges();
    public DataTable? GetChanges() => _inner.GetChanges();
    public DataTable? GetChanges(DataRowState rowStates) => _inner.GetChanges(rowStates);
    public void Clear() => _inner.Clear();
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

    // Sync I/O - delegate to _inner
    public void Load(IDataReader reader) => _inner.Load(reader);
    public void Load(IDataReader reader, LoadOption loadOption) => _inner.Load(reader, loadOption);
    public XmlReadMode ReadXml(Stream stream) => _inner.ReadXml(stream);
    public void ReadXmlSchema(Stream stream) => _inner.ReadXmlSchema(stream);
    public void WriteXml(Stream stream) => _inner.WriteXml(stream);
    public void WriteXml(Stream stream, XmlWriteMode mode) => _inner.WriteXml(stream, mode);
    public void WriteXmlSchema(Stream stream) => _inner.WriteXmlSchema(stream);

    // Async loading from IAsyncDataReader
    public async ValueTask<int> LoadAsync(IAsyncDataReader reader, CancellationToken cancellationToken = default)
    {
        return await LoadAsync(reader, LoadOption.OverwriteChanges, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> LoadAsync(IAsyncDataReader reader, LoadOption loadOption, CancellationToken cancellationToken = default)
    {
        // Build columns from schema if table has no columns yet
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

    // Events
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
}
