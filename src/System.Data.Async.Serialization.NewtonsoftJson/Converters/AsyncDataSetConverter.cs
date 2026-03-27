using System.Collections;
using System.Data.Async.DataSet;
using System.Globalization;

using Newtonsoft.Json;

namespace System.Data.Async.Converters;

/// <summary>
/// JSON converter for <see cref="AsyncDataSet"/> compatible with the Json.Net.DataSetConverters format.
/// </summary>
public sealed class AsyncDataSetConverter : JsonConverter<AsyncDataSet>
{
    public override AsyncDataSet? ReadJson(JsonReader reader, Type objectType, AsyncDataSet? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        var dataSet = new System.Data.DataSet();

        reader.Read(); // Move into object

        while (reader.TokenType == JsonToken.PropertyName)
        {
            var propertyName = (string)reader.Value!;

            switch (propertyName)
            {
                case "CaseSensitive":
                    dataSet.CaseSensitive = reader.ReadAsBoolean()!.Value;
                    break;
                case "DataSetName":
                    dataSet.DataSetName = reader.ReadAsString() ?? string.Empty;
                    break;
                case "EnforceConstraints":
                    dataSet.EnforceConstraints = reader.ReadAsBoolean()!.Value;
                    break;
                case "ExtendedProperties":
                    ReadExtendedProperties(reader, dataSet.ExtendedProperties);
                    break;
                case "Locale":
                    var locale = reader.ReadAsString() ?? string.Empty;
                    dataSet.Locale = string.IsNullOrEmpty(locale) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale);
                    break;
                case "Namespace":
                    dataSet.Namespace = reader.ReadAsString() ?? string.Empty;
                    break;
                case "Prefix":
                    dataSet.Prefix = reader.ReadAsString() ?? string.Empty;
                    break;
                case "RemotingFormat":
                    dataSet.RemotingFormat = (SerializationFormat)reader.ReadAsInt32()!.Value;
                    break;
                case "SchemaSerializationMode":
                    dataSet.SchemaSerializationMode = (SchemaSerializationMode)reader.ReadAsInt32()!.Value;
                    break;
                case "Tables":
                    ReadTables(reader, dataSet);
                    break;
                case "Relations":
                    ReadRelations(reader, dataSet);
                    break;
                default:
                    reader.Read();
                    reader.Skip();
                    break;
            }

            reader.Read(); // Move to next property or end object
        }

        return new AsyncDataSet(dataSet);
    }

    public override void WriteJson(JsonWriter writer, AsyncDataSet? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var dataSet = (System.Data.DataSet)value;

        writer.WriteStartObject();

        writer.WritePropertyName("CaseSensitive");
        writer.WriteValue(dataSet.CaseSensitive);

        writer.WritePropertyName("DataSetName");
        writer.WriteValue(dataSet.DataSetName);

        writer.WritePropertyName("EnforceConstraints");
        writer.WriteValue(dataSet.EnforceConstraints);

        WriteExtendedProperties(writer, dataSet.ExtendedProperties);

        writer.WritePropertyName("Locale");
        writer.WriteValue(dataSet.Locale == CultureInfo.InvariantCulture ? string.Empty : dataSet.Locale.Name);

        writer.WritePropertyName("Namespace");
        writer.WriteValue(dataSet.Namespace);

        writer.WritePropertyName("Prefix");
        writer.WriteValue(dataSet.Prefix);

        writer.WritePropertyName("RemotingFormat");
        writer.WriteValue((int)dataSet.RemotingFormat);

        writer.WritePropertyName("SchemaSerializationMode");
        writer.WriteValue((int)dataSet.SchemaSerializationMode);

        // Tables as object with table names as keys
        writer.WritePropertyName("Tables");
        writer.WriteStartObject();
        foreach (DataTable table in dataSet.Tables)
        {
            writer.WritePropertyName(table.TableName);
            AsyncDataTableConverter.WriteDataTable(writer, table);
        }

        writer.WriteEndObject();

        // Relations
        writer.WritePropertyName("Relations");
        writer.WriteStartObject();
        foreach (DataRelation relation in dataSet.Relations)
        {
            writer.WritePropertyName(relation.RelationName);
            WriteRelation(writer, relation);
        }

        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void WriteRelation(JsonWriter writer, DataRelation relation)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("RelationName");
        writer.WriteValue(relation.RelationName);

        writer.WritePropertyName("ChildTable");
        writer.WriteValue(relation.ChildTable.TableName);

        writer.WritePropertyName("ChildColumns");
        writer.WriteStartArray();
        foreach (var col in relation.ChildColumns)
        {
            writer.WriteValue(col.ColumnName);
        }

        writer.WriteEndArray();

        writer.WritePropertyName("Nested");
        writer.WriteValue(relation.Nested);

        writer.WritePropertyName("ParentTable");
        writer.WriteValue(relation.ParentTable.TableName);

        writer.WritePropertyName("ParentColumns");
        writer.WriteStartArray();
        foreach (var col in relation.ParentColumns)
        {
            writer.WriteValue(col.ColumnName);
        }

        writer.WriteEndArray();

        // ParentKeyConstraint
        writer.WritePropertyName("ParentKeyConstraint");
        if (relation.ParentKeyConstraint is not null)
        {
            writer.WriteValue(relation.ParentKeyConstraint.ConstraintName);
        }
        else
        {
            writer.WriteNull();
        }

        // ChildKeyConstraint
        writer.WritePropertyName("ChildKeyConstraint");
        if (relation.ChildKeyConstraint is not null)
        {
            var fk = relation.ChildKeyConstraint;
            writer.WriteStartObject();

            writer.WritePropertyName("AcceptRejectRule");
            writer.WriteValue((int)fk.AcceptRejectRule);

            writer.WritePropertyName("ConstraintName");
            writer.WriteValue(fk.ConstraintName);

            writer.WritePropertyName("DeleteRule");
            writer.WriteValue((int)fk.DeleteRule);

            writer.WritePropertyName("UpdateRule");
            writer.WriteValue((int)fk.UpdateRule);

            WriteExtendedProperties(writer, fk.ExtendedProperties);

            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull();
        }

        WriteExtendedProperties(writer, relation.ExtendedProperties);

        writer.WriteEndObject();
    }

    private static void ReadTables(JsonReader reader, System.Data.DataSet dataSet)
    {
        reader.Read(); // Move to StartObject
        reader.Read(); // Move to first property name or EndObject

        while (reader.TokenType == JsonToken.PropertyName)
        {
            reader.Read(); // Move to StartObject of table
            var table = AsyncDataTableConverter.ReadDataTable(reader);
            dataSet.Tables.Add(table);
            reader.Read(); // Move to next property name or EndObject
        }
    }

    private static void ReadRelations(JsonReader reader, System.Data.DataSet dataSet)
    {
        reader.Read(); // Move to StartObject
        reader.Read(); // Move to first property name or EndObject

        while (reader.TokenType == JsonToken.PropertyName)
        {
            reader.Read(); // Move to StartObject of relation
            ReadRelation(reader, dataSet);
            reader.Read(); // Move to next property name or EndObject
        }
    }

    private static void ReadRelation(JsonReader reader, System.Data.DataSet dataSet)
    {
        string relationName = string.Empty;
        string childTable = string.Empty;
        string[] childColumns = [];
        bool nested = false;
        string parentTable = string.Empty;
        string[] parentColumns = [];
        string? parentKeyConstraint = null;
        ForeignKeyConstraintInfo? childKeyConstraint = null;
        PropertyCollection? extendedProperties = null;

        reader.Read(); // Move into object

        while (reader.TokenType == JsonToken.PropertyName)
        {
            var propName = (string)reader.Value!;

            switch (propName)
            {
                case "RelationName":
                    relationName = reader.ReadAsString() ?? string.Empty;
                    break;
                case "ChildTable":
                    childTable = reader.ReadAsString() ?? string.Empty;
                    break;
                case "ChildColumns":
                    childColumns = ReadStringArray(reader);
                    break;
                case "Nested":
                    nested = reader.ReadAsBoolean()!.Value;
                    break;
                case "ParentTable":
                    parentTable = reader.ReadAsString() ?? string.Empty;
                    break;
                case "ParentColumns":
                    parentColumns = ReadStringArray(reader);
                    break;
                case "ParentKeyConstraint":
                    parentKeyConstraint = reader.ReadAsString();
                    break;
                case "ChildKeyConstraint":
                    reader.Read();
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        childKeyConstraint = ReadForeignKeyConstraintInfo(reader);
                    }

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

        // Create the relation
        var parentDataTable = dataSet.Tables[parentTable]!;
        var childDataTable = dataSet.Tables[childTable]!;

        var parentCols = parentColumns.Select(c => parentDataTable.Columns[c]!).ToArray();
        var childCols = childColumns.Select(c => childDataTable.Columns[c]!).ToArray();

        var relation = new DataRelation(relationName, parentCols, childCols, createConstraints: false);
        relation.Nested = nested;

        dataSet.Relations.Add(relation);

        // Apply child key constraint properties if available
        if (childKeyConstraint is not null && relation.ChildKeyConstraint is not null)
        {
            var fk = relation.ChildKeyConstraint;
            fk.AcceptRejectRule = childKeyConstraint.AcceptRejectRule;
            fk.ConstraintName = childKeyConstraint.ConstraintName;
            fk.DeleteRule = childKeyConstraint.DeleteRule;
            fk.UpdateRule = childKeyConstraint.UpdateRule;

            if (childKeyConstraint.ExtendedProperties is not null)
            {
                foreach (DictionaryEntry entry in childKeyConstraint.ExtendedProperties)
                {
                    fk.ExtendedProperties[entry.Key] = entry.Value;
                }
            }
        }

        if (extendedProperties is not null)
        {
            foreach (DictionaryEntry entry in extendedProperties)
            {
                relation.ExtendedProperties[entry.Key] = entry.Value;
            }
        }
    }

    private static string[] ReadStringArray(JsonReader reader)
    {
        reader.Read(); // StartArray
        var list = new List<string>();
        reader.Read(); // First value or EndArray
        while (reader.TokenType != JsonToken.EndArray)
        {
            list.Add((string)reader.Value!);
            reader.Read();
        }

        return [.. list];
    }

    private static ForeignKeyConstraintInfo ReadForeignKeyConstraintInfo(JsonReader reader)
    {
        var info = new ForeignKeyConstraintInfo();

        reader.Read(); // Move into object

        while (reader.TokenType == JsonToken.PropertyName)
        {
            var propName = (string)reader.Value!;

            switch (propName)
            {
                case "AcceptRejectRule":
                    info.AcceptRejectRule = (AcceptRejectRule)reader.ReadAsInt32()!.Value;
                    break;
                case "ConstraintName":
                    info.ConstraintName = reader.ReadAsString() ?? string.Empty;
                    break;
                case "DeleteRule":
                    info.DeleteRule = (Rule)reader.ReadAsInt32()!.Value;
                    break;
                case "UpdateRule":
                    info.UpdateRule = (Rule)reader.ReadAsInt32()!.Value;
                    break;
                case "ExtendedProperties":
                    info.ExtendedProperties = new PropertyCollection();
                    ReadExtendedPropertiesInto(reader, info.ExtendedProperties);
                    break;
                default:
                    reader.Read();
                    reader.Skip();
                    break;
            }

            reader.Read();
        }

        return info;
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

            reader.Read();
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

            reader.Read();
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

    private sealed class ForeignKeyConstraintInfo
    {
        public AcceptRejectRule AcceptRejectRule { get; set; }
        public string ConstraintName { get; set; } = string.Empty;
        public Rule DeleteRule { get; set; }
        public Rule UpdateRule { get; set; }
        public PropertyCollection? ExtendedProperties { get; set; }
    }
}
