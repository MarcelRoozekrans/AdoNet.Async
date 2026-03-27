using System.Collections;
using System.Data.Async.DataSet;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace System.Data.Async.Converters.SystemTextJson;

public sealed class AsyncDataTableJsonConverter : JsonConverter<AsyncDataTable>
{
    public override AsyncDataTable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var table = new DataTable();
        string? deferredDisplayExpression = null;

        reader.Read(); // into object

        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            var propertyName = reader.GetString()!;
            reader.Read(); // to value

            switch (propertyName)
            {
                case "CaseSensitive":
                    table.CaseSensitive = reader.GetBoolean();
                    break;
                case "DisplayExpression":
                    // Deferred: expression references columns, apply after columns are read
                    deferredDisplayExpression = reader.GetString() ?? string.Empty;
                    break;
                case "Locale":
                    var locale = reader.GetString() ?? string.Empty;
                    table.Locale = string.IsNullOrEmpty(locale)
                        ? CultureInfo.InvariantCulture
                        : CultureInfo.GetCultureInfo(locale);
                    break;
                case "MinimumCapacity":
                    table.MinimumCapacity = reader.GetInt32();
                    break;
                case "Namespace":
                    table.Namespace = reader.GetString() ?? string.Empty;
                    break;
                case "Prefix":
                    table.Prefix = reader.GetString() ?? string.Empty;
                    break;
                case "RemotingFormat":
                    table.RemotingFormat = (SerializationFormat)reader.GetInt32();
                    break;
                case "TableName":
                    table.TableName = reader.GetString() ?? string.Empty;
                    break;
                case "Columns":
                    ReadColumns(ref reader, table);
                    break;
                case "Constraints":
                    ReadConstraints(ref reader, table);
                    break;
                case "Rows":
                    ReadRows(ref reader, table);
                    break;
                default:
                    reader.Skip();
                    break;
            }

            reader.Read(); // next property or EndObject
        }

        if (!string.IsNullOrEmpty(deferredDisplayExpression))
            table.DisplayExpression = deferredDisplayExpression;

        return new AsyncDataTable(table);
    }

    public override void Write(Utf8JsonWriter writer, AsyncDataTable? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        WriteDataTable(writer, (DataTable)value);
    }

    internal static void WriteDataTable(Utf8JsonWriter writer, DataTable table)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("CaseSensitive", table.CaseSensitive);
        writer.WriteString("DisplayExpression", table.DisplayExpression);
        writer.WriteString("Locale", table.Locale == CultureInfo.InvariantCulture ? string.Empty : table.Locale.Name);
        writer.WriteNumber("MinimumCapacity", table.MinimumCapacity);
        writer.WriteString("Namespace", table.Namespace);
        writer.WriteString("Prefix", table.Prefix);
        writer.WriteNumber("RemotingFormat", (int)table.RemotingFormat);
        writer.WriteString("TableName", table.TableName);
        WriteColumns(writer, table);
        WriteConstraints(writer, table);
        WriteRows(writer, table);
        writer.WriteEndObject();
    }

    private static void WriteColumns(Utf8JsonWriter writer, DataTable table)
    {
        writer.WriteStartArray("Columns");
        foreach (DataColumn col in table.Columns)
        {
            writer.WriteStartObject();
            writer.WriteBoolean("AllowDBNull", col.AllowDBNull);
            writer.WriteBoolean("AutoIncrement", col.AutoIncrement);
            writer.WriteNumber("AutoIncrementSeed", col.AutoIncrementSeed);
            writer.WriteNumber("AutoIncrementStep", col.AutoIncrementStep);
            writer.WriteString("Caption", col.Caption);
            writer.WriteNumber("ColumnMapping", (int)col.ColumnMapping);
            writer.WriteString("ColumnName", col.ColumnName);
            writer.WriteString("DataType", col.DataType.AssemblyQualifiedName);
            writer.WriteNumber("DateTimeMode", (int)col.DateTimeMode);
            writer.WritePropertyName("DefaultValue");
            WriteColumnValue(writer, col.DefaultValue, col.DataType);
            writer.WriteString("Expression", col.Expression);
            WriteExtendedProperties(writer, col.ExtendedProperties);
            writer.WriteNumber("MaxLength", col.MaxLength);
            writer.WriteString("Namespace", col.Namespace);
            writer.WriteString("Prefix", col.Prefix);
            writer.WriteBoolean("ReadOnly", col.ReadOnly);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void ReadColumns(ref Utf8JsonReader reader, DataTable table)
    {
        // reader is at StartArray
        reader.Read(); // first StartObject or EndArray
        while (reader.TokenType == JsonTokenType.StartObject)
        {
            var col = new DataColumn();
            Type dataType = typeof(string);
            reader.Read(); // into object
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var prop = reader.GetString()!;
                reader.Read();
                switch (prop)
                {
                    case "AllowDBNull": col.AllowDBNull = reader.GetBoolean(); break;
                    case "AutoIncrement": col.AutoIncrement = reader.GetBoolean(); break;
                    case "AutoIncrementSeed": col.AutoIncrementSeed = reader.GetInt64(); break;
                    case "AutoIncrementStep": col.AutoIncrementStep = reader.GetInt64(); break;
                    case "Caption": col.Caption = reader.GetString() ?? string.Empty; break;
                    case "ColumnMapping": col.ColumnMapping = (MappingType)reader.GetInt32(); break;
                    case "ColumnName": col.ColumnName = reader.GetString() ?? string.Empty; break;
                    case "DataType":
                        var typeName = reader.GetString()!;
                        dataType = Type.GetType(typeName) ?? typeof(string);
                        col.DataType = dataType;
                        break;
                    case "DateTimeMode": col.DateTimeMode = (DataSetDateTime)reader.GetInt32(); break;
                    case "DefaultValue":
                        col.DefaultValue = reader.TokenType == JsonTokenType.Null
                            ? DBNull.Value
                            : ConvertValue(ref reader, dataType);
                        break;
                    case "Expression": col.Expression = reader.GetString() ?? string.Empty; break;
                    case "ExtendedProperties": ReadExtendedProperties(ref reader, col.ExtendedProperties); break;
                    case "MaxLength": col.MaxLength = reader.GetInt32(); break;
                    case "Namespace": col.Namespace = reader.GetString() ?? string.Empty; break;
                    case "Prefix": col.Prefix = reader.GetString() ?? string.Empty; break;
                    case "ReadOnly": col.ReadOnly = reader.GetBoolean(); break;
                    default: reader.Skip(); break;
                }
                reader.Read(); // next prop or EndObject
            }
            table.Columns.Add(col);
            reader.Read(); // past EndObject
        }
        // reader is at EndArray — caller's reader.Read() will advance past it
    }

    private static void WriteConstraints(Utf8JsonWriter writer, DataTable table)
    {
        writer.WriteStartArray("Constraints");
        foreach (Constraint constraint in table.Constraints)
        {
            if (constraint is UniqueConstraint uc)
            {
                writer.WriteStartObject();
                writer.WriteStartArray("Columns");
                foreach (var col in uc.Columns) writer.WriteStringValue(col.ColumnName);
                writer.WriteEndArray();
                writer.WriteString("ConstraintName", uc.ConstraintName);
                writer.WriteBoolean("IsPrimaryKey", uc.IsPrimaryKey);
                WriteExtendedProperties(writer, uc.ExtendedProperties);
                writer.WriteEndObject();
            }
        }
        writer.WriteEndArray();
    }

    private static void ReadConstraints(ref Utf8JsonReader reader, DataTable table)
    {
        reader.Read(); // StartArray → first StartObject or EndArray
        while (reader.TokenType == JsonTokenType.StartObject)
        {
            var colNames = Array.Empty<string>();
            string constraintName = string.Empty;
            bool isPrimaryKey = false;
            PropertyCollection? extProps = null;

            reader.Read(); // into object
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var prop = reader.GetString()!;
                reader.Read();
                switch (prop)
                {
                    case "Columns":
                        var list = new List<string>();
                        reader.Read(); // first value or EndArray
                        while (reader.TokenType != JsonTokenType.EndArray)
                        {
                            list.Add(reader.GetString()!);
                            reader.Read();
                        }
                        colNames = [.. list];
                        break;
                    case "ConstraintName": constraintName = reader.GetString() ?? string.Empty; break;
                    case "IsPrimaryKey": isPrimaryKey = reader.GetBoolean(); break;
                    case "ExtendedProperties":
                        extProps = new PropertyCollection();
                        ReadExtendedProperties(ref reader, extProps);
                        break;
                    default: reader.Skip(); break;
                }
                reader.Read();
            }

            var dataCols = new DataColumn[colNames.Length];
            for (int i = 0; i < colNames.Length; i++)
                dataCols[i] = table.Columns[colNames[i]]!;
            var uc = new UniqueConstraint(constraintName, dataCols, isPrimaryKey);
            if (extProps is not null)
                foreach (DictionaryEntry entry in extProps)
                    uc.ExtendedProperties[entry.Key] = entry.Value;

            table.Constraints.Add(uc);
            reader.Read(); // past EndObject
        }
    }

    private static void WriteRows(Utf8JsonWriter writer, DataTable table)
    {
        writer.WriteStartArray("Rows");
        foreach (DataRow row in table.Rows)
        {
            writer.WriteStartObject();
            var currentVersion = row.HasVersion(DataRowVersion.Proposed)
                ? DataRowVersion.Proposed
                : DataRowVersion.Current;

            switch (row.RowState)
            {
                case DataRowState.Unchanged:
                case DataRowState.Added:
                case DataRowState.Detached:
                    writer.WriteNull("OriginalRow");
                    WriteRowValues(writer, row, currentVersion);
                    writer.WriteNumber("RowState", (int)row.RowState);
                    break;

                case DataRowState.Modified:
                    writer.WriteStartObject("OriginalRow");
                    WriteRowValues(writer, row, DataRowVersion.Original);
                    writer.WriteNumber("RowState", (int)DataRowState.Modified);
                    writer.WriteEndObject();
                    WriteRowValues(writer, row, currentVersion);
                    writer.WriteNumber("RowState", (int)DataRowState.Modified);
                    break;

                case DataRowState.Deleted:
                    writer.WriteStartObject("OriginalRow");
                    WriteRowValues(writer, row, DataRowVersion.Original);
                    writer.WriteNumber("RowState", (int)DataRowState.Deleted);
                    writer.WriteEndObject();
                    WriteRowValues(writer, row, DataRowVersion.Original);
                    writer.WriteNumber("RowState", (int)DataRowState.Deleted);
                    break;
            }

            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void ReadRows(ref Utf8JsonReader reader, DataTable table)
    {
        var readOnlyColumns = new List<DataColumn>();
        foreach (DataColumn col in table.Columns)
        {
            if (col.ReadOnly && string.IsNullOrEmpty(col.Expression))
            {
                readOnlyColumns.Add(col);
                col.ReadOnly = false;
            }
        }

        try
        {
            reader.Read(); // StartArray → first StartObject or EndArray
            while (reader.TokenType == JsonTokenType.StartObject)
            {
                reader.Read(); // into object, first property should be "OriginalRow"

                DataRow? originalRow = null;
                if (reader.TokenType == JsonTokenType.PropertyName &&
                    string.Equals(reader.GetString(), "OriginalRow", StringComparison.Ordinal))
                {
                    reader.Read(); // to value
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        originalRow = table.NewRow();
                        reader.Read(); // into OriginalRow object
                        while (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            var colName = reader.GetString()!;
                            reader.Read();
                            if (string.Equals(colName, "RowState", StringComparison.Ordinal))
                            {
                                reader.Skip(); // discard inner RowState
                            }
                            else if (table.Columns.Contains(colName))
                            {
                                var col = table.Columns[colName]!;
                                originalRow[colName] = reader.TokenType == JsonTokenType.Null
                                    ? DBNull.Value
                                    : ConvertValue(ref reader, col.DataType);
                            }
                            else reader.Skip();
                            reader.Read();
                        }
                        reader.Read(); // past EndObject of OriginalRow
                    }
                    else
                    {
                        reader.Read(); // null OriginalRow — advance past null value
                    }
                }

                if (originalRow is not null)
                {
                    table.Rows.Add(originalRow);
                    originalRow.AcceptChanges();
                }

                var currentRow = originalRow ?? table.NewRow();

                while (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var colName = reader.GetString()!;
                    if (string.Equals(colName, "RowState", StringComparison.Ordinal))
                        break;
                    reader.Read();
                    if (table.Columns.Contains(colName))
                    {
                        var col = table.Columns[colName]!;
                        currentRow[colName] = reader.TokenType == JsonTokenType.Null
                            ? DBNull.Value
                            : ConvertValue(ref reader, col.DataType);
                    }
                    else reader.Skip();
                    reader.Read();
                }

                int rowState = (int)DataRowState.Unchanged;
                if (reader.TokenType == JsonTokenType.PropertyName &&
                    string.Equals(reader.GetString(), "RowState", StringComparison.Ordinal))
                {
                    reader.Read();
                    rowState = reader.GetInt32();
                    reader.Read(); // past RowState value
                }

                if (originalRow is null)
                    table.Rows.Add(currentRow);

                switch ((DataRowState)rowState)
                {
                    case DataRowState.Unchanged:
                        currentRow.AcceptChanges();
                        break;
                    case DataRowState.Added:
                    case DataRowState.Detached: // Detached deserializes as Added
                        currentRow.AcceptChanges();
                        currentRow.SetAdded();
                        break;
                    case DataRowState.Modified:
                        break; // already modified (current differs from original)
                    case DataRowState.Deleted:
                        currentRow.Delete();
                        break;
                }

                reader.Read(); // past EndObject of row
            }
        }
        finally
        {
            foreach (ref var col in CollectionsMarshal.AsSpan(readOnlyColumns))
                col.ReadOnly = true;
        }
    }

    private static void WriteRowValues(Utf8JsonWriter writer, DataRow row, DataRowVersion version)
    {
        foreach (DataColumn col in row.Table.Columns)
        {
            writer.WritePropertyName(col.ColumnName);
            WriteColumnValue(writer, row[col, version], col.DataType);
        }
    }

    internal static void WriteColumnValue(Utf8JsonWriter writer, object? value, Type dataType)
    {
        if (value is null || value == DBNull.Value) { writer.WriteNullValue(); return; }
        if (dataType == typeof(decimal))
        {
            writer.WriteStringValue(((decimal)value).ToString("F28", CultureInfo.InvariantCulture));
            return;
        }
        if (dataType == typeof(byte[]))
        {
            writer.WriteBase64StringValue((byte[])value);
            return;
        }
        if (value is bool b) { writer.WriteBooleanValue(b); return; }
        if (value is int i) { writer.WriteNumberValue(i); return; }
        if (value is long l) { writer.WriteNumberValue(l); return; }
        if (value is double d) { writer.WriteNumberValue(d); return; }
        if (value is float f) { writer.WriteNumberValue(f); return; }
        if (value is short sh) { writer.WriteNumberValue(sh); return; }
        if (value is byte by) { writer.WriteNumberValue(by); return; }
        if (value is uint ui) { writer.WriteNumberValue(ui); return; }
        if (value is ulong ul) { writer.WriteNumberValue(ul); return; }
        if (value is sbyte sb) { writer.WriteNumberValue(sb); return; }
        if (value is ushort us) { writer.WriteNumberValue(us); return; }
        if (value is DateTime dt) { writer.WriteStringValue(dt); return; }
        if (value is DateTimeOffset dto) { writer.WriteStringValue(dto); return; }
        if (value is Guid g) { writer.WriteStringValue(g); return; }
        if (value is TimeSpan ts) { writer.WriteStringValue(ts.ToString("c", CultureInfo.InvariantCulture)); return; }
        writer.WriteStringValue(value.ToString());
    }

    internal static object ConvertValue(ref Utf8JsonReader reader, Type targetType)
    {
        if (targetType == typeof(decimal))
        {
            var s = reader.GetString()!;
            return decimal.Parse(s, CultureInfo.InvariantCulture);
        }
        if (targetType == typeof(byte[]))
            return reader.GetBytesFromBase64();
        if (targetType == typeof(bool))
            return reader.GetBoolean();
        if (targetType == typeof(int))
            return reader.TokenType == JsonTokenType.Number ? reader.GetInt32() : int.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        if (targetType == typeof(long))
            return reader.TokenType == JsonTokenType.Number ? reader.GetInt64() : long.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        if (targetType == typeof(double))
            return reader.TokenType == JsonTokenType.Number ? reader.GetDouble() : double.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        if (targetType == typeof(float))
            return reader.TokenType == JsonTokenType.Number ? (float)reader.GetDouble() : float.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        if (targetType == typeof(DateTime))
            return reader.GetDateTime();
        if (targetType == typeof(DateTimeOffset))
            return reader.TokenType == JsonTokenType.String ? DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture) : reader.GetDateTimeOffset();
        if (targetType == typeof(Guid))
            return reader.GetGuid();
        if (targetType == typeof(TimeSpan))
            return TimeSpan.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        return Convert.ChangeType(
            reader.TokenType == JsonTokenType.String ? reader.GetString()! : reader.GetInt64().ToString(CultureInfo.InvariantCulture),
            targetType, CultureInfo.InvariantCulture);
    }

    internal static void WriteExtendedProperties(Utf8JsonWriter writer, PropertyCollection properties)
    {
        writer.WriteStartArray("ExtendedProperties");
        foreach (DictionaryEntry entry in properties)
        {
            writer.WriteStartObject();
            writer.WriteString("KeyType", entry.Key.GetType().FullName);
            writer.WriteString("Key", entry.Key.ToString());
            writer.WriteString("ValueType", entry.Value?.GetType().FullName ?? "System.String");
            writer.WriteString("Value", entry.Value?.ToString());
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    internal static void ReadExtendedProperties(ref Utf8JsonReader reader, PropertyCollection properties)
    {
        reader.Read(); // past StartArray, to first StartObject or EndArray
        while (reader.TokenType == JsonTokenType.StartObject)
        {
            string keyType = "System.String", key = string.Empty, valueType = "System.String";
            string? value = null;
            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var prop = reader.GetString()!;
                reader.Read();
                switch (prop)
                {
                    case "KeyType": keyType = reader.GetString() ?? "System.String"; break;
                    case "Key": key = reader.GetString() ?? string.Empty; break;
                    case "ValueType": valueType = reader.GetString() ?? "System.String"; break;
                    case "Value": value = reader.GetString(); break;
                    default: reader.Skip(); break;
                }
                reader.Read();
            }
            var keyObj = ConvertExtendedPropertyValue(key, keyType);
            var valueObj = value is null ? null : ConvertExtendedPropertyValue(value, valueType);
            properties[keyObj] = valueObj;
            reader.Read(); // past EndObject
        }
    }

    private static object ConvertExtendedPropertyValue(string value, string typeName)
    {
        var type = Type.GetType(typeName);
        if (type is null || type == typeof(string)) return value;
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
}
