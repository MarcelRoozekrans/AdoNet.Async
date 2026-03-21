using System.Collections;
using System.Data.Async.DataSet;
using System.Globalization;

using Newtonsoft.Json;

namespace System.Data.Async.Converters;

/// <summary>
/// JSON converter for <see cref="AsyncDataTable"/> compatible with the Json.Net.DataSetConverters format.
/// </summary>
public sealed class AsyncDataTableConverter : JsonConverter<AsyncDataTable>
{
    public override AsyncDataTable? ReadJson(JsonReader reader, Type objectType, AsyncDataTable? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        var table = new DataTable();

        reader.Read(); // Move into object

        while (reader.TokenType == JsonToken.PropertyName)
        {
            var propertyName = (string)reader.Value!;

            switch (propertyName)
            {
                case "CaseSensitive":
                    table.CaseSensitive = reader.ReadAsBoolean()!.Value;
                    break;
                case "DisplayExpression":
                    table.DisplayExpression = reader.ReadAsString() ?? string.Empty;
                    break;
                case "Locale":
                    var locale = reader.ReadAsString() ?? string.Empty;
                    table.Locale = string.IsNullOrEmpty(locale) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale);
                    break;
                case "MinimumCapacity":
                    table.MinimumCapacity = reader.ReadAsInt32()!.Value;
                    break;
                case "Namespace":
                    table.Namespace = reader.ReadAsString() ?? string.Empty;
                    break;
                case "Prefix":
                    table.Prefix = reader.ReadAsString() ?? string.Empty;
                    break;
                case "RemotingFormat":
                    table.RemotingFormat = (SerializationFormat)reader.ReadAsInt32()!.Value;
                    break;
                case "TableName":
                    table.TableName = reader.ReadAsString() ?? string.Empty;
                    break;
                case "Columns":
                    ReadColumns(reader, table);
                    break;
                case "Constraints":
                    ReadConstraints(reader, table);
                    break;
                case "Rows":
                    ReadRows(reader, table);
                    break;
                default:
                    reader.Read();
                    reader.Skip();
                    break;
            }

            reader.Read(); // Move to next property or end object
        }

        return new AsyncDataTable(table);
    }

    public override void WriteJson(JsonWriter writer, AsyncDataTable? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var table = value.InnerDataTable;

        writer.WriteStartObject();

        writer.WritePropertyName("CaseSensitive");
        writer.WriteValue(table.CaseSensitive);

        writer.WritePropertyName("DisplayExpression");
        writer.WriteValue(table.DisplayExpression);

        writer.WritePropertyName("Locale");
        writer.WriteValue(table.Locale == CultureInfo.InvariantCulture ? string.Empty : table.Locale.Name);

        writer.WritePropertyName("MinimumCapacity");
        writer.WriteValue(table.MinimumCapacity);

        writer.WritePropertyName("Namespace");
        writer.WriteValue(table.Namespace);

        writer.WritePropertyName("Prefix");
        writer.WriteValue(table.Prefix);

        writer.WritePropertyName("RemotingFormat");
        writer.WriteValue((int)table.RemotingFormat);

        writer.WritePropertyName("TableName");
        writer.WriteValue(table.TableName);

        WriteColumns(writer, table);
        WriteConstraints(writer, table);
        WriteRows(writer, table);

        writer.WriteEndObject();
    }

    internal static void WriteDataTable(JsonWriter writer, DataTable table)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("CaseSensitive");
        writer.WriteValue(table.CaseSensitive);

        writer.WritePropertyName("DisplayExpression");
        writer.WriteValue(table.DisplayExpression);

        writer.WritePropertyName("Locale");
        writer.WriteValue(table.Locale == CultureInfo.InvariantCulture ? string.Empty : table.Locale.Name);

        writer.WritePropertyName("MinimumCapacity");
        writer.WriteValue(table.MinimumCapacity);

        writer.WritePropertyName("Namespace");
        writer.WriteValue(table.Namespace);

        writer.WritePropertyName("Prefix");
        writer.WriteValue(table.Prefix);

        writer.WritePropertyName("RemotingFormat");
        writer.WriteValue((int)table.RemotingFormat);

        writer.WritePropertyName("TableName");
        writer.WriteValue(table.TableName);

        WriteColumns(writer, table);
        WriteConstraints(writer, table);
        WriteRows(writer, table);

        writer.WriteEndObject();
    }

    internal static DataTable ReadDataTable(JsonReader reader)
    {
        var table = new DataTable();

        reader.Read(); // Move into object

        while (reader.TokenType == JsonToken.PropertyName)
        {
            var propertyName = (string)reader.Value!;

            switch (propertyName)
            {
                case "CaseSensitive":
                    table.CaseSensitive = reader.ReadAsBoolean()!.Value;
                    break;
                case "DisplayExpression":
                    table.DisplayExpression = reader.ReadAsString() ?? string.Empty;
                    break;
                case "Locale":
                    var locale = reader.ReadAsString() ?? string.Empty;
                    table.Locale = string.IsNullOrEmpty(locale) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale);
                    break;
                case "MinimumCapacity":
                    table.MinimumCapacity = reader.ReadAsInt32()!.Value;
                    break;
                case "Namespace":
                    table.Namespace = reader.ReadAsString() ?? string.Empty;
                    break;
                case "Prefix":
                    table.Prefix = reader.ReadAsString() ?? string.Empty;
                    break;
                case "RemotingFormat":
                    table.RemotingFormat = (SerializationFormat)reader.ReadAsInt32()!.Value;
                    break;
                case "TableName":
                    table.TableName = reader.ReadAsString() ?? string.Empty;
                    break;
                case "Columns":
                    ReadColumns(reader, table);
                    break;
                case "Constraints":
                    ReadConstraints(reader, table);
                    break;
                case "Rows":
                    ReadRows(reader, table);
                    break;
                default:
                    reader.Read();
                    reader.Skip();
                    break;
            }

            reader.Read(); // Move to next property or end object
        }

        return table;
    }

    private static void WriteColumns(JsonWriter writer, DataTable table)
    {
        writer.WritePropertyName("Columns");
        writer.WriteStartArray();

        foreach (DataColumn col in table.Columns)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("AllowDBNull");
            writer.WriteValue(col.AllowDBNull);

            writer.WritePropertyName("AutoIncrement");
            writer.WriteValue(col.AutoIncrement);

            writer.WritePropertyName("AutoIncrementSeed");
            writer.WriteValue(col.AutoIncrementSeed);

            writer.WritePropertyName("AutoIncrementStep");
            writer.WriteValue(col.AutoIncrementStep);

            writer.WritePropertyName("Caption");
            writer.WriteValue(col.Caption);

            writer.WritePropertyName("ColumnMapping");
            writer.WriteValue((int)col.ColumnMapping);

            writer.WritePropertyName("ColumnName");
            writer.WriteValue(col.ColumnName);

            writer.WritePropertyName("DataType");
            writer.WriteValue(col.DataType.FullName);

            writer.WritePropertyName("DateTimeMode");
            writer.WriteValue((int)col.DateTimeMode);

            writer.WritePropertyName("DefaultValue");
            WriteColumnValue(writer, col.DefaultValue, col.DataType);

            writer.WritePropertyName("Expression");
            writer.WriteValue(col.Expression);

            WriteExtendedProperties(writer, col.ExtendedProperties);

            writer.WritePropertyName("MaxLength");
            writer.WriteValue(col.MaxLength);

            writer.WritePropertyName("Namespace");
            writer.WriteValue(col.Namespace);

            writer.WritePropertyName("Prefix");
            writer.WriteValue(col.Prefix);

            writer.WritePropertyName("ReadOnly");
            writer.WriteValue(col.ReadOnly);

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void ReadColumns(JsonReader reader, DataTable table)
    {
        reader.Read(); // Move to StartArray
        reader.Read(); // Move to first StartObject or EndArray

        while (reader.TokenType == JsonToken.StartObject)
        {
            var col = new DataColumn();
            Type dataType = typeof(string);

            reader.Read(); // Move into column object

            while (reader.TokenType == JsonToken.PropertyName)
            {
                var propName = (string)reader.Value!;

                switch (propName)
                {
                    case "AllowDBNull":
                        col.AllowDBNull = reader.ReadAsBoolean()!.Value;
                        break;
                    case "AutoIncrement":
                        col.AutoIncrement = reader.ReadAsBoolean()!.Value;
                        break;
                    case "AutoIncrementSeed":
                        col.AutoIncrementSeed = (long)reader.ReadAsInt32()!.Value;
                        break;
                    case "AutoIncrementStep":
                        col.AutoIncrementStep = (long)reader.ReadAsInt32()!.Value;
                        break;
                    case "Caption":
                        col.Caption = reader.ReadAsString() ?? string.Empty;
                        break;
                    case "ColumnMapping":
                        col.ColumnMapping = (MappingType)reader.ReadAsInt32()!.Value;
                        break;
                    case "ColumnName":
                        col.ColumnName = reader.ReadAsString() ?? string.Empty;
                        break;
                    case "DataType":
                        var typeName = reader.ReadAsString()!;
                        dataType = Type.GetType(typeName) ?? typeof(string);
                        col.DataType = dataType;
                        break;
                    case "DateTimeMode":
                        col.DateTimeMode = (DataSetDateTime)reader.ReadAsInt32()!.Value;
                        break;
                    case "DefaultValue":
                        reader.Read();
                        col.DefaultValue = reader.TokenType == JsonToken.Null
                            ? DBNull.Value
                            : ConvertValue(reader.Value!, dataType);
                        break;
                    case "Expression":
                        col.Expression = reader.ReadAsString() ?? string.Empty;
                        break;
                    case "ExtendedProperties":
                        ReadExtendedProperties(reader, col.ExtendedProperties);
                        break;
                    case "MaxLength":
                        col.MaxLength = reader.ReadAsInt32()!.Value;
                        break;
                    case "Namespace":
                        col.Namespace = reader.ReadAsString() ?? string.Empty;
                        break;
                    case "Prefix":
                        col.Prefix = reader.ReadAsString() ?? string.Empty;
                        break;
                    case "ReadOnly":
                        col.ReadOnly = reader.ReadAsBoolean()!.Value;
                        break;
                    default:
                        reader.Read();
                        reader.Skip();
                        break;
                }

                reader.Read(); // Move to next property or EndObject
            }

            table.Columns.Add(col);
            reader.Read(); // Move past EndObject to next StartObject or EndArray
        }
    }

    private static void WriteConstraints(JsonWriter writer, DataTable table)
    {
        writer.WritePropertyName("Constraints");
        writer.WriteStartArray();

        foreach (Constraint constraint in table.Constraints)
        {
            if (constraint is UniqueConstraint uc)
            {
                writer.WriteStartObject();

                writer.WritePropertyName("Columns");
                writer.WriteStartArray();
                foreach (var col in uc.Columns)
                {
                    writer.WriteValue(col.ColumnName);
                }

                writer.WriteEndArray();

                writer.WritePropertyName("ConstraintName");
                writer.WriteValue(uc.ConstraintName);

                writer.WritePropertyName("IsPrimaryKey");
                writer.WriteValue(uc.IsPrimaryKey);

                WriteExtendedProperties(writer, uc.ExtendedProperties);

                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray();
    }

    private static void ReadConstraints(JsonReader reader, DataTable table)
    {
        reader.Read(); // Move to StartArray
        reader.Read(); // Move to first StartObject or EndArray

        while (reader.TokenType == JsonToken.StartObject)
        {
            string[] columns = [];
            string constraintName = string.Empty;
            bool isPrimaryKey = false;
            PropertyCollection? extendedProperties = null;

            reader.Read(); // Move into object

            while (reader.TokenType == JsonToken.PropertyName)
            {
                var propName = (string)reader.Value!;

                switch (propName)
                {
                    case "Columns":
                        reader.Read(); // StartArray
                        var colList = new List<string>();
                        reader.Read(); // First value or EndArray
                        while (reader.TokenType != JsonToken.EndArray)
                        {
                            colList.Add((string)reader.Value!);
                            reader.Read();
                        }

                        columns = [.. colList];
                        break;
                    case "ConstraintName":
                        constraintName = reader.ReadAsString() ?? string.Empty;
                        break;
                    case "IsPrimaryKey":
                        isPrimaryKey = reader.ReadAsBoolean()!.Value;
                        break;
                    case "ExtendedProperties":
                        extendedProperties = new PropertyCollection();
                        ReadExtendedPropertiesInto(reader, extendedProperties);
                        break;
                    default:
                        reader.Read();
                        reader.Skip();
                        break;
                }

                reader.Read(); // Move to next property or EndObject
            }

            var dataCols = columns.Select(c => table.Columns[c]!).ToArray();
            var uc = new UniqueConstraint(constraintName, dataCols, isPrimaryKey);

            if (extendedProperties is not null)
            {
                foreach (DictionaryEntry entry in extendedProperties)
                {
                    uc.ExtendedProperties[entry.Key] = entry.Value;
                }
            }

            table.Constraints.Add(uc);

            reader.Read(); // Move past EndObject to next StartObject or EndArray
        }
    }

    private static void WriteRows(JsonWriter writer, DataTable table)
    {
        writer.WritePropertyName("Rows");
        writer.WriteStartArray();

        foreach (DataRow row in table.Rows)
        {
            writer.WriteStartObject();

            switch (row.RowState)
            {
                case DataRowState.Unchanged:
                case DataRowState.Added:
                    writer.WritePropertyName("OriginalRow");
                    writer.WriteNull();
                    WriteRowValues(writer, row, DataRowVersion.Current);
                    writer.WritePropertyName("RowState");
                    writer.WriteValue((int)row.RowState);
                    break;

                case DataRowState.Modified:
                    writer.WritePropertyName("OriginalRow");
                    writer.WriteStartObject();
                    WriteRowValues(writer, row, DataRowVersion.Original);
                    writer.WritePropertyName("RowState");
                    writer.WriteValue((int)DataRowState.Modified);
                    writer.WriteEndObject();
                    WriteRowValues(writer, row, DataRowVersion.Current);
                    writer.WritePropertyName("RowState");
                    writer.WriteValue((int)DataRowState.Modified);
                    break;

                case DataRowState.Deleted:
                    writer.WritePropertyName("OriginalRow");
                    writer.WriteStartObject();
                    WriteRowValues(writer, row, DataRowVersion.Original);
                    writer.WritePropertyName("RowState");
                    writer.WriteValue((int)DataRowState.Deleted);
                    writer.WriteEndObject();
                    WriteRowValues(writer, row, DataRowVersion.Original);
                    writer.WritePropertyName("RowState");
                    writer.WriteValue((int)DataRowState.Deleted);
                    break;
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void ReadRows(JsonReader reader, DataTable table)
    {
        // Track which columns are read-only so we can temporarily make them writable
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
            reader.Read(); // Move to StartArray
            reader.Read(); // Move to first StartObject or EndArray

            while (reader.TokenType == JsonToken.StartObject)
            {
                reader.Read(); // Move to first property name

                // Read OriginalRow
                DataRow? originalRow = null;
                if (reader.TokenType == JsonToken.PropertyName && string.Equals((string)reader.Value!, "OriginalRow", StringComparison.Ordinal))
                {
                    reader.Read(); // Read value
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        // Read original row values
                        originalRow = table.NewRow();
                        reader.Read(); // Move into object
                        while (reader.TokenType == JsonToken.PropertyName)
                        {
                            var propName = (string)reader.Value!;
                            if (string.Equals(propName, "RowState", StringComparison.Ordinal))
                            {
                                reader.Read(); // skip value
                                reader.Read(); // move past it
                                continue;
                            }

                            reader.Read();
                            if (table.Columns.Contains(propName))
                            {
                                var col = table.Columns[propName]!;
                                originalRow[propName] = reader.TokenType == JsonToken.Null
                                    ? DBNull.Value
                                    : ConvertValue(reader.Value!, col.DataType);
                            }

                            reader.Read(); // Move to next prop or EndObject
                        }

                        reader.Read(); // Move past EndObject of OriginalRow
                    }
                    else
                    {
                        // null OriginalRow
                        reader.Read(); // Move to next property
                    }
                }

                // If we had an original row, add it and accept changes
                if (originalRow is not null)
                {
                    table.Rows.Add(originalRow);
                    originalRow.AcceptChanges(); // Now Unchanged
                }

                // Read current column values
                var currentRow = originalRow;
                if (currentRow is null)
                {
                    currentRow = table.NewRow();
                }

                while (reader.TokenType == JsonToken.PropertyName)
                {
                    var propName = (string)reader.Value!;
                    if (string.Equals(propName, "RowState", StringComparison.Ordinal))
                    {
                        break;
                    }

                    reader.Read();
                    if (table.Columns.Contains(propName))
                    {
                        var col = table.Columns[propName]!;
                        currentRow[propName] = reader.TokenType == JsonToken.Null
                            ? DBNull.Value
                            : ConvertValue(reader.Value!, col.DataType);
                    }

                    reader.Read(); // Move to next prop
                }

                // Read RowState
                int rowState = 2; // Default: Unchanged
                if (reader.TokenType == JsonToken.PropertyName && string.Equals((string)reader.Value!, "RowState", StringComparison.Ordinal))
                {
                    rowState = reader.ReadAsInt32()!.Value;
                    reader.Read(); // Move past RowState value to EndObject
                }

                if (originalRow is null)
                {
                    // Row was not yet added
                    table.Rows.Add(currentRow);
                }

                // Adjust row state
                switch ((DataRowState)rowState)
                {
                    case DataRowState.Unchanged:
                        currentRow.AcceptChanges();
                        break;
                    case DataRowState.Added:
                        currentRow.AcceptChanges();
                        currentRow.SetAdded();
                        break;
                    case DataRowState.Modified:
                        // Row already modified because current values differ from original
                        break;
                    case DataRowState.Deleted:
                        currentRow.Delete();
                        break;
                }

                reader.Read(); // Move past EndObject to next StartObject or EndArray
            }
        }
        finally
        {
            // Restore read-only state
            foreach (var col in readOnlyColumns)
            {
                col.ReadOnly = true;
            }
        }
    }

    private static void WriteRowValues(JsonWriter writer, DataRow row, DataRowVersion version)
    {
        foreach (DataColumn col in row.Table.Columns)
        {
            writer.WritePropertyName(col.ColumnName);
            var value = row[col, version];
            WriteColumnValue(writer, value, col.DataType);
        }
    }

    private static void WriteColumnValue(JsonWriter writer, object? value, Type dataType)
    {
        if (value is null || value == DBNull.Value)
        {
            writer.WriteNull();
        }
        else if (dataType == typeof(decimal))
        {
            writer.WriteValue(((decimal)value).ToString("F28", CultureInfo.InvariantCulture));
        }
        else if (dataType == typeof(byte[]))
        {
            writer.WriteValue(Convert.ToBase64String((byte[])value));
        }
        else
        {
            writer.WriteValue(value);
        }
    }

    private static void WriteExtendedProperties(JsonWriter writer, PropertyCollection properties)
    {
        writer.WritePropertyName("ExtendedProperties");
        writer.WriteStartArray();

        foreach (DictionaryEntry entry in properties)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("KeyType");
            writer.WriteValue(entry.Key.GetType().FullName);

            writer.WritePropertyName("Key");
            writer.WriteValue(entry.Key.ToString());

            writer.WritePropertyName("ValueType");
            writer.WriteValue(entry.Value?.GetType().FullName ?? "System.String");

            writer.WritePropertyName("Value");
            writer.WriteValue(entry.Value?.ToString());

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void ReadExtendedProperties(JsonReader reader, PropertyCollection properties)
    {
        reader.Read(); // Move to StartArray
        reader.Read(); // Move to first StartObject or EndArray

        while (reader.TokenType == JsonToken.StartObject)
        {
            string keyType = "System.String";
            string key = string.Empty;
            string valueType = "System.String";
            string? value = null;

            reader.Read(); // Move into object
            while (reader.TokenType == JsonToken.PropertyName)
            {
                var propName = (string)reader.Value!;
                switch (propName)
                {
                    case "KeyType":
                        keyType = reader.ReadAsString() ?? "System.String";
                        break;
                    case "Key":
                        key = reader.ReadAsString() ?? string.Empty;
                        break;
                    case "ValueType":
                        valueType = reader.ReadAsString() ?? "System.String";
                        break;
                    case "Value":
                        value = reader.ReadAsString();
                        break;
                    default:
                        reader.Read();
                        reader.Skip();
                        break;
                }

                reader.Read();
            }

            var keyObj = ConvertExtendedPropertyValue(key, keyType);
            var valueObj = value is null ? null : ConvertExtendedPropertyValue(value, valueType);
            properties[keyObj] = valueObj;

            reader.Read(); // Move past EndObject
        }
    }

    private static void ReadExtendedPropertiesInto(JsonReader reader, PropertyCollection properties)
    {
        ReadExtendedProperties(reader, properties);
    }

    private static object ConvertExtendedPropertyValue(string value, string typeName)
    {
        var type = Type.GetType(typeName);
        if (type is null || type == typeof(string))
        {
            return value;
        }

        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    private static object ConvertValue(object value, Type targetType)
    {
        if (value is null || value == DBNull.Value)
        {
            return DBNull.Value;
        }

        if (targetType == typeof(decimal))
        {
            return value is string s
                ? decimal.Parse(s, CultureInfo.InvariantCulture)
                : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(byte[]) && value is string base64)
        {
            return Convert.FromBase64String(base64);
        }

        if (targetType == typeof(Guid) && value is string guidStr)
        {
            return Guid.Parse(guidStr);
        }

        if (targetType == typeof(TimeSpan) && value is string tsStr)
        {
            return TimeSpan.Parse(tsStr, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(DateTime) && value is DateTime dt)
        {
            return dt;
        }

        if (targetType == typeof(DateTimeOffset) && value is DateTime dto)
        {
            return new DateTimeOffset(dto);
        }

        if (targetType == typeof(DateTimeOffset) && value is DateTimeOffset dtoVal)
        {
            return dtoVal;
        }

        try
        {
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (InvalidCastException)
        {
            return value;
        }
    }
}
