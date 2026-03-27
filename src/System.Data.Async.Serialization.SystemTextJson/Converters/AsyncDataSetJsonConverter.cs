using System.Collections;
using System.Data.Async.DataSet;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace System.Data.Async.Converters.SystemTextJson;

public sealed class AsyncDataSetJsonConverter : JsonConverter<AsyncDataSet>
{
    public override AsyncDataSet? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var ds = new System.Data.DataSet();
        reader.Read(); // into object

        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            var prop = reader.GetString()!;
            reader.Read();
            switch (prop)
            {
                case "CaseSensitive": ds.CaseSensitive = reader.GetBoolean(); break;
                case "DataSetName": ds.DataSetName = reader.GetString() ?? string.Empty; break;
                case "EnforceConstraints": ds.EnforceConstraints = reader.GetBoolean(); break;
                case "ExtendedProperties": AsyncDataTableJsonConverter.ReadExtendedProperties(ref reader, ds.ExtendedProperties); break;
                case "Locale":
                    var locale = reader.GetString() ?? string.Empty;
                    ds.Locale = string.IsNullOrEmpty(locale) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale);
                    break;
                case "Namespace": ds.Namespace = reader.GetString() ?? string.Empty; break;
                case "Prefix": ds.Prefix = reader.GetString() ?? string.Empty; break;
                case "RemotingFormat": ds.RemotingFormat = (SerializationFormat)reader.GetInt32(); break;
                case "SchemaSerializationMode": ds.SchemaSerializationMode = (SchemaSerializationMode)reader.GetInt32(); break;
                case "Tables": ReadTables(ref reader, ds, options); break;
                case "Relations": ReadRelations(ref reader, ds); break;
                default: reader.Skip(); break;
            }
            reader.Read();
        }

        return new AsyncDataSet(ds);
    }

    public override void Write(Utf8JsonWriter writer, AsyncDataSet? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }

        var ds = value.InnerDataSet;
        writer.WriteStartObject();
        writer.WriteBoolean("CaseSensitive", ds.CaseSensitive);
        writer.WriteString("DataSetName", ds.DataSetName);
        writer.WriteBoolean("EnforceConstraints", ds.EnforceConstraints);
        AsyncDataTableJsonConverter.WriteExtendedProperties(writer, ds.ExtendedProperties);
        writer.WriteString("Locale", ds.Locale == CultureInfo.InvariantCulture ? string.Empty : ds.Locale.Name);
        writer.WriteString("Namespace", ds.Namespace);
        writer.WriteString("Prefix", ds.Prefix);
        writer.WriteNumber("RemotingFormat", (int)ds.RemotingFormat);
        writer.WriteNumber("SchemaSerializationMode", (int)ds.SchemaSerializationMode);

        writer.WriteStartObject("Tables");
        foreach (DataTable table in ds.Tables)
        {
            writer.WritePropertyName(table.TableName);
            AsyncDataTableJsonConverter.WriteDataTable(writer, table);
        }
        writer.WriteEndObject();

        writer.WriteStartObject("Relations");
        foreach (DataRelation relation in ds.Relations)
        {
            writer.WritePropertyName(relation.RelationName);
            WriteRelation(writer, relation);
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void ReadTables(ref Utf8JsonReader reader, System.Data.DataSet ds, JsonSerializerOptions options)
    {
        // reader is at StartObject (tables keyed by name)
        reader.Read(); // first PropertyName or EndObject
        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            reader.Read(); // to table StartObject
            var tableConverter = new AsyncDataTableJsonConverter();
            var asyncTable = tableConverter.Read(ref reader, typeof(AsyncDataTable), options);
            if (asyncTable is not null)
                ds.Tables.Add(asyncTable.InnerDataTable);
            reader.Read(); // next PropertyName or EndObject
        }
    }

    private static void ReadRelations(ref Utf8JsonReader reader, System.Data.DataSet ds)
    {
        reader.Read(); // first PropertyName or EndObject
        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            reader.Read(); // to relation StartObject
            ReadRelation(ref reader, ds);
            reader.Read(); // next PropertyName or EndObject
        }
    }

    private static void WriteRelation(Utf8JsonWriter writer, DataRelation relation)
    {
        writer.WriteStartObject();
        writer.WriteString("RelationName", relation.RelationName);
        writer.WriteString("ChildTable", relation.ChildTable.TableName);
        writer.WriteStartArray("ChildColumns");
        foreach (var col in relation.ChildColumns) writer.WriteStringValue(col.ColumnName);
        writer.WriteEndArray();
        writer.WriteBoolean("Nested", relation.Nested);
        writer.WriteString("ParentTable", relation.ParentTable.TableName);
        writer.WriteStartArray("ParentColumns");
        foreach (var col in relation.ParentColumns) writer.WriteStringValue(col.ColumnName);
        writer.WriteEndArray();

        if (relation.ParentKeyConstraint is not null)
            writer.WriteString("ParentKeyConstraint", relation.ParentKeyConstraint.ConstraintName);
        else
            writer.WriteNull("ParentKeyConstraint");

        if (relation.ChildKeyConstraint is not null)
        {
            var fk = relation.ChildKeyConstraint;
            writer.WriteStartObject("ChildKeyConstraint");
            writer.WriteNumber("AcceptRejectRule", (int)fk.AcceptRejectRule);
            writer.WriteString("ConstraintName", fk.ConstraintName);
            writer.WriteNumber("DeleteRule", (int)fk.DeleteRule);
            writer.WriteNumber("UpdateRule", (int)fk.UpdateRule);
            AsyncDataTableJsonConverter.WriteExtendedProperties(writer, fk.ExtendedProperties);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("ChildKeyConstraint");
        }

        AsyncDataTableJsonConverter.WriteExtendedProperties(writer, relation.ExtendedProperties);
        writer.WriteEndObject();
    }

    private static void ReadRelation(ref Utf8JsonReader reader, System.Data.DataSet ds)
    {
        string relationName = string.Empty, childTable = string.Empty, parentTable = string.Empty;
        string[] childColumns = [], parentColumns = [];
        bool nested = false;

        reader.Read(); // into object
        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            var prop = reader.GetString()!;
            reader.Read();
            switch (prop)
            {
                case "RelationName": relationName = reader.GetString() ?? string.Empty; break;
                case "ChildTable": childTable = reader.GetString() ?? string.Empty; break;
                case "ChildColumns": childColumns = ReadStringArray(ref reader); break;
                case "Nested": nested = reader.GetBoolean(); break;
                case "ParentTable": parentTable = reader.GetString() ?? string.Empty; break;
                case "ParentColumns": parentColumns = ReadStringArray(ref reader); break;
                default: reader.Skip(); break;
            }
            reader.Read();
        }

        var parent = ds.Tables[parentTable]!;
        var child = ds.Tables[childTable]!;
        var parentCols = new DataColumn[parentColumns.Length];
        for (int i = 0; i < parentColumns.Length; i++)
            parentCols[i] = parent.Columns[parentColumns[i]]!;
        var childCols = new DataColumn[childColumns.Length];
        for (int i = 0; i < childColumns.Length; i++)
            childCols[i] = child.Columns[childColumns[i]]!;
        var rel = new DataRelation(relationName, parentCols, childCols) { Nested = nested };
        ds.Relations.Add(rel);
    }

    private static string[] ReadStringArray(ref Utf8JsonReader reader)
    {
        var list = new List<string>();
        reader.Read(); // first value or EndArray
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(reader.GetString()!);
            reader.Read();
        }
        return [.. list];
    }

}
