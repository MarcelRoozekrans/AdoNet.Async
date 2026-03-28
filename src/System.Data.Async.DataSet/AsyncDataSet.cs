using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;

namespace System.Data.Async.DataSet;

public class AsyncDataSet : IDisposable
{
    private readonly System.Data.DataSet _inner;
    private readonly ConditionalWeakTable<DataTable, AsyncDataTable> _tableCache = new();
    private readonly AsyncDataTableCollection _tables;

    public AsyncDataSet()
    {
        _inner = new System.Data.DataSet();
        _tables = new AsyncDataTableCollection(_inner.Tables, this);
    }

    public AsyncDataSet(string dataSetName)
    {
        _inner = new System.Data.DataSet(dataSetName);
        _tables = new AsyncDataTableCollection(_inner.Tables, this);
    }

    public AsyncDataSet(System.Data.DataSet inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _tables = new AsyncDataTableCollection(_inner.Tables, this);
    }

    protected internal System.Data.DataSet InnerDataSet => _inner;

    protected virtual AsyncDataTable CreateTable(DataTable inner) => new(inner);

    internal AsyncDataTable GetOrCreateTable(DataTable inner)
    {
        return _tableCache.GetValue(inner, key =>
        {
            var t = CreateTable(key);
            t.ParentAsyncDataSet = this;
            return t;
        });
    }

    // Properties
    public string DataSetName { get => _inner.DataSetName; set => _inner.DataSetName = value; }
    public string Namespace { get => _inner.Namespace; set => _inner.Namespace = value; }
    public string Prefix { get => _inner.Prefix; set => _inner.Prefix = value; }
    public bool CaseSensitive { get => _inner.CaseSensitive; set => _inner.CaseSensitive = value; }
    public CultureInfo Locale { get => _inner.Locale; set => _inner.Locale = value; }
    public bool EnforceConstraints { get => _inner.EnforceConstraints; set => _inner.EnforceConstraints = value; }
    public bool HasErrors => _inner.HasErrors;
    public bool IsInitialized => _inner.IsInitialized;
    public SerializationFormat RemotingFormat { get => _inner.RemotingFormat; set => _inner.RemotingFormat = value; }
    public SchemaSerializationMode SchemaSerializationMode { get => _inner.SchemaSerializationMode; set => _inner.SchemaSerializationMode = value; }
    public DataViewManager DefaultViewManager => _inner.DefaultViewManager;

    // Collections
    public AsyncDataTableCollection Tables => _tables;
    public DataRelationCollection Relations => _inner.Relations;
    public PropertyCollection ExtendedProperties => _inner.ExtendedProperties;

    // Methods
    public void AcceptChanges() => _inner.AcceptChanges();
    public void RejectChanges() => _inner.RejectChanges();
    public bool HasChanges() => _inner.HasChanges();
    public bool HasChanges(DataRowState rowStates) => _inner.HasChanges(rowStates);
    public AsyncDataSet? GetChanges()
    {
        var changes = _inner.GetChanges();
        return changes != null ? new AsyncDataSet(changes) : null;
    }

    public AsyncDataSet? GetChanges(DataRowState rowStates)
    {
        var changes = _inner.GetChanges(rowStates);
        return changes != null ? new AsyncDataSet(changes) : null;
    }

    public void Clear() => _inner.Clear();
    public AsyncDataSet Clone() => new(_inner.Clone());
    public AsyncDataSet Copy() => new(_inner.Copy());

    // Merge overloads accepting async types
    public void Merge(AsyncDataSet dataSet) => _inner.Merge(dataSet._inner);
    public void Merge(AsyncDataTable table) => _inner.Merge(table.InnerDataTable);
    public void Merge(AsyncDataSet dataSet, bool preserveChanges) => _inner.Merge(dataSet._inner, preserveChanges);
    public void Merge(AsyncDataTable table, bool preserveChanges, MissingSchemaAction missingSchemaAction)
        => _inner.Merge(table.InnerDataTable, preserveChanges, missingSchemaAction);
    public void Merge(AsyncDataRow[] rows) => _inner.Merge(rows.Select(r => r.InnerDataRow).ToArray());

    // Merge overloads accepting raw types for backward compatibility
    public void Merge(System.Data.DataSet dataSet) => _inner.Merge(dataSet);
    public void Merge(DataTable table) => _inner.Merge(table);
    public void Merge(System.Data.DataSet dataSet, bool preserveChanges) => _inner.Merge(dataSet, preserveChanges);
    public void Merge(DataTable table, bool preserveChanges, MissingSchemaAction missingSchemaAction)
        => _inner.Merge(table, preserveChanges, missingSchemaAction);
    public void Merge(DataRow[] rows) => _inner.Merge(rows);
    public void Reset() => _inner.Reset();
    public void BeginInit() => _inner.BeginInit();
    public void EndInit() => _inner.EndInit();

    // Async XML I/O
    public ValueTask ReadXmlAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Note: DataSet.ReadXml does not support async I/O internally.
        // This method provides API consistency but executes synchronously.
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
        // Note: DataSet.ReadXmlSchema does not support async I/O internally.
        // This method provides API consistency but executes synchronously.
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

    // Sync I/O
#pragma warning disable CA5366 // Delegating to inner DataSet; callers control the stream
    public XmlReadMode ReadXml(Stream stream) => _inner.ReadXml(stream);
    public void ReadXmlSchema(Stream stream) => _inner.ReadXmlSchema(stream);
#pragma warning restore CA5366
    public void WriteXml(Stream stream) => _inner.WriteXml(stream);
    public void WriteXml(Stream stream, XmlWriteMode mode) => _inner.WriteXml(stream, mode);
    public void WriteXmlSchema(Stream stream) => _inner.WriteXmlSchema(stream);
    public string GetXml() => _inner.GetXml();
    public string GetXmlSchema() => _inner.GetXmlSchema();

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

    // Events
    public event MergeFailedEventHandler? MergeFailed
    {
        add => _inner.MergeFailed += value;
        remove => _inner.MergeFailed -= value;
    }

    // Explicit conversion
    public static explicit operator System.Data.DataSet(AsyncDataSet asyncDataSet) => asyncDataSet._inner;
}
