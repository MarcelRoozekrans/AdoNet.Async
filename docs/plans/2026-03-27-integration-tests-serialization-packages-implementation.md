# Integration Tests + Serialization Packages Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extract Newtonsoft.Json converters into a dedicated package, add a System.Text.Json package with identical wire format, and add an integration test project proving lossless interop and cross-serialization compatibility.

**Architecture:** Three new projects — `AdoNet.Async.Serialization.NewtonsoftJson` (moved converters + serialization fixes), `AdoNet.Async.Serialization.SystemTextJson` (STJ implementation), `System.Data.Async.Integration.Tests` (in-memory interop + serialization cross-compatibility). `AdoNet.Async.DataSet` drops its Newtonsoft.Json dependency entirely. All converters must produce the same wire format as `Json.Net.DataSetConverters`.

**Tech Stack:** .NET 10, C# preview, xUnit 2.x, FluentAssertions 8.x, Newtonsoft.Json 13.x, System.Text.Json (in-box), Json.Net.DataSetConverters 1.2.0

**Reference implementation:** `Json.Net.DataSetConverters` (https://github.com/AlesDo/DataSetConverters) — our wire format must match exactly.

**Key wire format facts:**
- `RowState` is stored as an **integer** (Newtonsoft.Json default enum serialization — no `StringEnumConverter`)
- `OriginalRow` is only non-null for `Modified` and `Deleted` rows
- Deleted rows duplicate original values in the current section
- `decimal` values serialized as `"F28"` strings
- `byte[]` values serialized as Base64 strings
- `DataRowVersion.Proposed` used as current values when row is in `BeginEdit`
- Detached rows serialize with `OriginalRow: null`; they deserialize as `Added`

---

### Task 1: Create `AdoNet.Async.Serialization.NewtonsoftJson` project

**Files:**
- Create: `src/System.Data.Async.Serialization.NewtonsoftJson/System.Data.Async.Serialization.NewtonsoftJson.csproj`

**Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>System.Data.Async.Serialization.NewtonsoftJson</RootNamespace>
    <PackageId>AdoNet.Async.Serialization.NewtonsoftJson</PackageId>
    <Title>AdoNet.Async.Serialization.NewtonsoftJson</Title>
    <Description>Newtonsoft.Json converters for AsyncDataTable and AsyncDataSet, wire-compatible with Json.Net.DataSetConverters.</Description>
    <PackageTags>system.data.async;async;ado.net;dataset;datatable;json;newtonsoft</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\System.Data.Async.DataSet\System.Data.Async.DataSet.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.*" />
  </ItemGroup>
</Project>
```

**Step 2: Copy converter files into new project**

Copy these two files verbatim into `src/System.Data.Async.Serialization.NewtonsoftJson/Converters/`:
- `src/System.Data.Async.DataSet/Converters/AsyncDataTableConverter.cs`
- `src/System.Data.Async.DataSet/Converters/AsyncDataSetConverter.cs`

Keep namespace `System.Data.Async.Converters` — no breaking change.

**Step 3: Add to solution**

Edit `System.Data.Async.slnx`, add the new project inside the `/src/` folder:

```xml
<Project Path="src/System.Data.Async.Serialization.NewtonsoftJson/System.Data.Async.Serialization.NewtonsoftJson.csproj" />
```

**Step 4: Verify it builds**

```bash
dotnet build src/System.Data.Async.Serialization.NewtonsoftJson
```
Expected: Build succeeded, 0 errors.

**Step 5: Commit**

```bash
git add src/System.Data.Async.Serialization.NewtonsoftJson/ System.Data.Async.slnx
git commit -m "feat: add AdoNet.Async.Serialization.NewtonsoftJson project with moved converters"
```

---

### Task 2: Fix Newtonsoft converter — `DataRowVersion.Proposed` + Detached rows

**Files:**
- Modify: `src/System.Data.Async.Serialization.NewtonsoftJson/Converters/AsyncDataTableConverter.cs`

**Background:** When a `DataRow` is in `BeginEdit()` (edit pending), it has `DataRowVersion.Proposed`. The current `WriteRows` always uses `DataRowVersion.Current`. Also `DataRowState.Detached` is not handled (falls through the switch). Both must match the reference implementation.

**Step 1: Replace `WriteRows` in `AsyncDataTableConverter.cs`**

Find the `private static void WriteRows(JsonWriter writer, DataTable table)` method and replace it entirely:

```csharp
private static void WriteRows(JsonWriter writer, DataTable table)
{
    writer.WritePropertyName("Rows");
    writer.WriteStartArray();

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
                writer.WritePropertyName("OriginalRow");
                writer.WriteNull();
                WriteRowValues(writer, row, currentVersion);
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
                WriteRowValues(writer, row, currentVersion);
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
```

**Step 2: Verify existing DataSet converter tests still pass**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests
```
Expected: All tests pass (the DataSet tests still reference the old location — that's fine for now).

**Step 3: Commit**

```bash
git add src/System.Data.Async.Serialization.NewtonsoftJson/
git commit -m "fix: handle DataRowVersion.Proposed and Detached rows in Newtonsoft converter"
```

---

### Task 3: Strip converters from `AdoNet.Async.DataSet`

**Files:**
- Delete: `src/System.Data.Async.DataSet/Converters/AsyncDataTableConverter.cs`
- Delete: `src/System.Data.Async.DataSet/Converters/AsyncDataSetConverter.cs`
- Modify: `src/System.Data.Async.DataSet/System.Data.Async.DataSet.csproj`
- Modify: `tests/System.Data.Async.DataSet.Tests/System.Data.Async.DataSet.Tests.csproj`

**Step 1: Delete the converter files from DataSet**

Delete both files:
- `src/System.Data.Async.DataSet/Converters/AsyncDataTableConverter.cs`
- `src/System.Data.Async.DataSet/Converters/AsyncDataSetConverter.cs`

**Step 2: Remove Newtonsoft.Json from DataSet csproj**

Edit `src/System.Data.Async.DataSet/System.Data.Async.DataSet.csproj`. Remove the entire `<PackageReference Include="Newtonsoft.Json" ...>` item and the `<ItemGroup>` block containing only that reference. Result:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>System.Data.Async.DataSet</RootNamespace>
    <PackageId>AdoNet.Async.DataSet</PackageId>
    <Title>AdoNet.Async.DataSet</Title>
    <Description>Async DataSet and DataTable for ADO.NET (System.Data). Includes AsyncDataTable, AsyncDataSet, and AsyncDataAdapter.</Description>
    <PackageTags>system.data.async;async;ado.net;dataset;datatable;dataadapter;valuetask</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\System.Data.Async\System.Data.Async.csproj" />
  </ItemGroup>
</Project>
```

**Step 3: Update DataSet.Tests to reference new serialization package**

Edit `tests/System.Data.Async.DataSet.Tests/System.Data.Async.DataSet.Tests.csproj`. Add reference to the new package:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\System.Data.Async.DataSet\System.Data.Async.DataSet.csproj" />
    <ProjectReference Include="..\..\src\System.Data.Async.Serialization.NewtonsoftJson\System.Data.Async.Serialization.NewtonsoftJson.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Json.Net.DataSetConverters" Version="1.2.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="FluentAssertions" Version="8.*" />
  </ItemGroup>
</Project>
```

**Step 4: Remove `CrossCompatibilityTests.cs` from DataSet.Tests**

Delete `tests/System.Data.Async.DataSet.Tests/CrossCompatibilityTests.cs` — the integration test project will supersede it with fuller coverage.

**Step 5: Build and run DataSet tests**

```bash
dotnet build src/System.Data.Async.DataSet
dotnet test tests/System.Data.Async.DataSet.Tests
```
Expected: Build succeeded, all tests pass.

**Step 6: Commit**

```bash
git add src/System.Data.Async.DataSet/ tests/System.Data.Async.DataSet.Tests/
git commit -m "refactor: remove Newtonsoft.Json from DataSet package, reference serialization package from tests"
```

---

### Task 4: Create `AdoNet.Async.Serialization.SystemTextJson` project

**Files:**
- Create: `src/System.Data.Async.Serialization.SystemTextJson/System.Data.Async.Serialization.SystemTextJson.csproj`

**Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>System.Data.Async.Serialization.SystemTextJson</RootNamespace>
    <PackageId>AdoNet.Async.Serialization.SystemTextJson</PackageId>
    <Title>AdoNet.Async.Serialization.SystemTextJson</Title>
    <Description>System.Text.Json converters for AsyncDataTable and AsyncDataSet, wire-compatible with Json.Net.DataSetConverters.</Description>
    <PackageTags>system.data.async;async;ado.net;dataset;datatable;json;system.text.json</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\System.Data.Async.DataSet\System.Data.Async.DataSet.csproj" />
  </ItemGroup>
</Project>
```

Note: No `System.Text.Json` package reference — it is in-box on .NET 10.

**Step 2: Add to solution**

Edit `System.Data.Async.slnx`, add inside `/src/` folder:

```xml
<Project Path="src/System.Data.Async.Serialization.SystemTextJson/System.Data.Async.Serialization.SystemTextJson.csproj" />
```

**Step 3: Commit**

```bash
git add src/System.Data.Async.Serialization.SystemTextJson/ System.Data.Async.slnx
git commit -m "feat: add AdoNet.Async.Serialization.SystemTextJson project (empty)"
```

---

### Task 5: Implement `AsyncDataTableJsonConverter` (System.Text.Json)

**Files:**
- Create: `src/System.Data.Async.Serialization.SystemTextJson/Converters/AsyncDataTableJsonConverter.cs`

**Background:** STJ uses `Utf8JsonReader` (ref struct, forward-only) and `Utf8JsonWriter`. Must produce the exact same JSON as `AsyncDataTableConverter` (Newtonsoft). Read order: all schema properties can come in any order because we use a switch; rows are read after columns. ReadOnly columns must be temporarily disabled during deserialization.

**Step 1: Write the converter**

Create `src/System.Data.Async.Serialization.SystemTextJson/Converters/AsyncDataTableJsonConverter.cs`:

```csharp
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
        WriteDataTable(writer, value.InnerDataTable);
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
        // reader is at EndArray — caller will Read() past it
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

            var dataCols = colNames.Select(n => table.Columns[n]!).ToArray();
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
                reader.Read(); // into object, should be "OriginalRow"

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
                                reader.Skip(); // discard
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
                        reader.Read(); // null OriginalRow — move past it
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
                        currentRow.AcceptChanges();
                        currentRow.SetAdded();
                        break;
                    case DataRowState.Modified:
                        break; // already modified (current differs from original)
                    case DataRowState.Deleted:
                        currentRow.Delete();
                        break;
                    default: // Detached and others → becomes Added (known limitation)
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

    private static void WriteColumnValue(Utf8JsonWriter writer, object? value, Type dataType)
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
        if (value is DateTime dt) { writer.WriteStringValue(dt); return; }
        if (value is DateTimeOffset dto) { writer.WriteStringValue(dto); return; }
        if (value is Guid g) { writer.WriteStringValue(g); return; }
        if (value is TimeSpan ts) { writer.WriteStringValue(ts.ToString("c", CultureInfo.InvariantCulture)); return; }
        writer.WriteStringValue(value.ToString());
    }

    private static object ConvertValue(ref Utf8JsonReader reader, Type targetType)
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

    private static void WriteExtendedProperties(Utf8JsonWriter writer, PropertyCollection properties)
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

    private static void ReadExtendedProperties(ref Utf8JsonReader reader, PropertyCollection properties)
    {
        reader.Read(); // StartArray
        reader.Read(); // first StartObject or EndArray
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
```

**Step 2: Build**

```bash
dotnet build src/System.Data.Async.Serialization.SystemTextJson
```
Expected: Build succeeded, 0 errors.

**Step 3: Commit**

```bash
git add src/System.Data.Async.Serialization.SystemTextJson/
git commit -m "feat: implement AsyncDataTableJsonConverter (System.Text.Json)"
```

---

### Task 6: Implement `AsyncDataSetJsonConverter` (System.Text.Json)

**Files:**
- Create: `src/System.Data.Async.Serialization.SystemTextJson/Converters/AsyncDataSetJsonConverter.cs`

**Background:** `AsyncDataSetConverter` (Newtonsoft) uses `WriteDataTable` / `ReadDataTable` from `AsyncDataTableConverter`. The STJ version does the same using `AsyncDataTableJsonConverter.WriteDataTable` / `ReadDataTable`. The DataSet wire format wraps tables in a JSON object keyed by `TableName`, and relations as a JSON object keyed by `RelationName`.

**Step 1: Write the converter**

Create `src/System.Data.Async.Serialization.SystemTextJson/Converters/AsyncDataSetJsonConverter.cs`:

```csharp
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
                case "ExtendedProperties": ReadExtendedProperties(ref reader, ds.ExtendedProperties); break;
                case "Locale":
                    var locale = reader.GetString() ?? string.Empty;
                    ds.Locale = string.IsNullOrEmpty(locale) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale);
                    break;
                case "Namespace": ds.Namespace = reader.GetString() ?? string.Empty; break;
                case "Prefix": ds.Prefix = reader.GetString() ?? string.Empty; break;
                case "RemotingFormat": ds.RemotingFormat = (SerializationFormat)reader.GetInt32(); break;
                case "SchemaSerializationMode": ds.SchemaSerializationMode = (SchemaSerializationMode)reader.GetInt32(); break;
                case "Tables": ReadTables(ref reader, ds); break;
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
        WriteExtendedProperties(writer, ds.ExtendedProperties);
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

    private static void ReadTables(ref Utf8JsonReader reader, System.Data.DataSet ds)
    {
        // reader is at StartObject (tables keyed by name)
        reader.Read(); // first PropertyName or EndObject
        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            reader.Read(); // to table StartObject
            var asyncTable = new AsyncDataTableJsonConverter().Read(ref reader, typeof(AsyncDataTable), new JsonSerializerOptions());
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
            WriteExtendedProperties(writer, fk.ExtendedProperties);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("ChildKeyConstraint");
        }

        WriteExtendedProperties(writer, relation.ExtendedProperties);
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
        var parentCols = parentColumns.Select(n => parent.Columns[n]!).ToArray();
        var childCols = childColumns.Select(n => child.Columns[n]!).ToArray();
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

    private static void WriteExtendedProperties(Utf8JsonWriter writer, PropertyCollection properties)
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

    private static void ReadExtendedProperties(ref Utf8JsonReader reader, PropertyCollection properties)
    {
        reader.Read(); // StartArray
        reader.Read(); // first StartObject or EndArray
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
            properties[ConvertValue(key, keyType)] = value is null ? null : ConvertValue(value, valueType);
            reader.Read();
        }
    }

    private static object ConvertValue(string value, string typeName)
    {
        var type = Type.GetType(typeName);
        if (type is null || type == typeof(string)) return value;
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
}
```

**Step 2: Build**

```bash
dotnet build src/System.Data.Async.Serialization.SystemTextJson
```
Expected: Build succeeded, 0 errors.

**Step 3: Commit**

```bash
git add src/System.Data.Async.Serialization.SystemTextJson/
git commit -m "feat: implement AsyncDataSetJsonConverter (System.Text.Json)"
```

---

### Task 7: Create integration test project

**Files:**
- Create: `tests/System.Data.Async.Integration.Tests/System.Data.Async.Integration.Tests.csproj`

**Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\System.Data.Async.DataSet\System.Data.Async.DataSet.csproj" />
    <ProjectReference Include="..\..\src\System.Data.Async.Serialization.NewtonsoftJson\System.Data.Async.Serialization.NewtonsoftJson.csproj" />
    <ProjectReference Include="..\..\src\System.Data.Async.Serialization.SystemTextJson\System.Data.Async.Serialization.SystemTextJson.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Json.Net.DataSetConverters" Version="1.2.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="FluentAssertions" Version="8.*" />
  </ItemGroup>
</Project>
```

**Step 2: Add to solution**

Edit `System.Data.Async.slnx`, add inside `/tests/` folder:

```xml
<Project Path="tests/System.Data.Async.Integration.Tests/System.Data.Async.Integration.Tests.csproj" />
```

**Step 3: Build**

```bash
dotnet build tests/System.Data.Async.Integration.Tests
```
Expected: Build succeeded, 0 errors.

**Step 4: Commit**

```bash
git add tests/System.Data.Async.Integration.Tests/ System.Data.Async.slnx
git commit -m "feat: add System.Data.Async.Integration.Tests project"
```

---

### Task 8: Write `DataTableInteropTests`

**Files:**
- Create: `tests/System.Data.Async.Integration.Tests/DataTableInteropTests.cs`

**Background:** These tests prove that wrapping a `DataTable` in `AsyncDataTable` (and unwrapping) is lossless. No serialization — pure in-memory interop. `AsyncDataTable(DataTable inner)` wraps by reference; `InnerDataTable` unwraps.

**Step 1: Write the tests**

```csharp
using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Integration.Tests;

public class DataTableInteropTests
{
    private static DataTable BuildRichTable()
    {
        var dt = new DataTable("Orders");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        dt.Columns.Add("Amount", typeof(decimal));
        dt.PrimaryKey = [dt.Columns["Id"]!];

        dt.Rows.Add(1, "Unchanged", 10m);
        dt.Rows.Add(2, "WillModify", 20m);
        dt.Rows.Add(3, "WillDelete", 30m);
        dt.AcceptChanges();

        dt.Rows.Add(4, "Added", 40m);         // Added
        dt.Rows[1]["Name"] = "Modified";       // Modified
        dt.Rows[2].Delete();                   // Deleted
        return dt;
    }

    [Fact]
    public void Wrap_And_Unwrap_Preserves_All_Data()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);

        wrapped.InnerDataTable.Should().BeSameAs(dt);
        wrapped.TableName.Should().Be("Orders");
        wrapped.Rows.Count.Should().Be(4); // Deleted rows still counted
    }

    [Fact]
    public void Wrap_Preserves_Column_Schema()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);

        wrapped.Columns.Count.Should().Be(3);
        wrapped.Columns["Id"]!.DataType.Should().Be(typeof(int));
        wrapped.Columns["Name"]!.DataType.Should().Be(typeof(string));
        wrapped.Columns["Amount"]!.DataType.Should().Be(typeof(decimal));
    }

    [Fact]
    public void Wrap_Preserves_PrimaryKey()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);
        wrapped.PrimaryKey.Should().HaveCount(1);
        wrapped.PrimaryKey[0].ColumnName.Should().Be("Id");
    }

    [Fact]
    public void Wrap_Preserves_All_Row_States()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);

        wrapped.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        wrapped.Rows[1].RowState.Should().Be(DataRowState.Modified);
        wrapped.Rows[2].RowState.Should().Be(DataRowState.Deleted);
        wrapped.Rows[3].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void Wrap_Preserves_Modified_Original_Values()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);

        wrapped.Rows[1]["Name"].Should().Be("Modified");
        wrapped.Rows[1]["Name", DataRowVersion.Original].Should().Be("WillModify");
    }

    [Fact]
    public void Wrap_Preserves_Constraints()
    {
        var dt = new DataTable("T");
        dt.Columns.Add("Id", typeof(int));
        dt.Constraints.Add(new UniqueConstraint("UQ_Id", dt.Columns["Id"]!, isPrimaryKey: true));

        var wrapped = new AsyncDataTable(dt);
        wrapped.Constraints.Count.Should().Be(1);
        ((UniqueConstraint)wrapped.Constraints[0]).IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Wrap_Preserves_Extended_Properties()
    {
        var dt = new DataTable("T");
        dt.ExtendedProperties["Author"] = "Test";

        var wrapped = new AsyncDataTable(dt);
        wrapped.ExtendedProperties["Author"].Should().Be("Test");
    }
}
```

**Step 2: Run tests**

```bash
dotnet test tests/System.Data.Async.Integration.Tests --filter "FullyQualifiedName~DataTableInteropTests"
```
Expected: All 7 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Integration.Tests/DataTableInteropTests.cs
git commit -m "test: add DataTableInteropTests (in-memory wrap/unwrap)"
```

---

### Task 9: Write `DataSetInteropTests`

**Files:**
- Create: `tests/System.Data.Async.Integration.Tests/DataSetInteropTests.cs`

**Step 1: Write the tests**

```csharp
using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Integration.Tests;

public class DataSetInteropTests
{
    private static System.Data.DataSet BuildRichDataSet()
    {
        var ds = new System.Data.DataSet("Shop");
        ds.CaseSensitive = false;

        var customers = ds.Tables.Add("Customers");
        customers.Columns.Add("CustomerId", typeof(int));
        customers.Columns.Add("Name", typeof(string));
        customers.PrimaryKey = [customers.Columns["CustomerId"]!];

        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("OrderId", typeof(int));
        orders.Columns.Add("CustomerId", typeof(int));
        orders.Columns.Add("Total", typeof(decimal));
        orders.PrimaryKey = [orders.Columns["OrderId"]!];

        ds.Relations.Add("CustomerOrders",
            customers.Columns["CustomerId"]!,
            orders.Columns["CustomerId"]!);

        customers.Rows.Add(1, "Alice");
        customers.Rows.Add(2, "Bob");
        orders.Rows.Add(100, 1, 99.99m);
        orders.Rows.Add(101, 2, 149.50m);
        ds.AcceptChanges();

        customers.Rows[1]["Name"] = "Robert";     // Modified
        orders.Rows.Add(102, 1, 25.00m);          // Added
        return ds;
    }

    [Fact]
    public void Wrap_And_Unwrap_Preserves_DataSetName()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        wrapped.InnerDataSet.Should().BeSameAs(ds);
        wrapped.DataSetName.Should().Be("Shop");
    }

    [Fact]
    public void Wrap_Preserves_All_Tables()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        wrapped.Tables.Count.Should().Be(2);
        wrapped.Tables["Customers"].Should().NotBeNull();
        wrapped.Tables["Orders"].Should().NotBeNull();
    }

    [Fact]
    public void Wrap_Preserves_Relations()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        wrapped.Relations.Count.Should().Be(1);
        wrapped.Relations["CustomerOrders"]!.ParentTable.TableName.Should().Be("Customers");
        wrapped.Relations["CustomerOrders"]!.ChildTable.TableName.Should().Be("Orders");
    }

    [Fact]
    public void Wrap_Preserves_Row_States_Across_Tables()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);

        wrapped.Tables["Customers"]!.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        wrapped.Tables["Customers"]!.Rows[1].RowState.Should().Be(DataRowState.Modified);
        wrapped.Tables["Orders"]!.Rows[2].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void Wrap_Preserves_CaseSensitive_Flag()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        wrapped.CaseSensitive.Should().BeFalse();
    }

    [Fact]
    public void Wrap_Preserves_PrimaryKeys()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        wrapped.Tables["Customers"]!.PrimaryKey.Should().HaveCount(1);
        wrapped.Tables["Customers"]!.PrimaryKey[0].ColumnName.Should().Be("CustomerId");
    }
}
```

**Step 2: Run tests**

```bash
dotnet test tests/System.Data.Async.Integration.Tests --filter "FullyQualifiedName~DataSetInteropTests"
```
Expected: All 6 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Integration.Tests/DataSetInteropTests.cs
git commit -m "test: add DataSetInteropTests (in-memory wrap/unwrap)"
```

---

### Task 10: Write `NewtonsoftJsonCrossCompatibilityTests`

**Files:**
- Create: `tests/System.Data.Async.Integration.Tests/NewtonsoftJsonCrossCompatibilityTests.cs`

**Background:** These tests use `Json.Net.DataSetConverters` as the reference serializer and prove our `AsyncDataTableConverter`/`AsyncDataSetConverter` produce and consume the same JSON. Helper methods reduce repetition.

**Step 1: Write the tests**

```csharp
using System.Data.Async.Converters;
using System.Data.Async.DataSet;
using FluentAssertions;
using Json.Net.DataSetConverters;
using Newtonsoft.Json;
using Xunit;

namespace System.Data.Async.Integration.Tests;

public class NewtonsoftJsonCrossCompatibilityTests
{
    // --- Settings helpers ---

    private static JsonSerializerSettings ReferenceSettings() => new JsonSerializerSettings
    {
        Converters = { new DataTableConverter(), new DataSetConverter() }
    };

    private static JsonSerializerSettings AsyncSettings() => new JsonSerializerSettings
    {
        Converters = { new AsyncDataTableConverter(), new AsyncDataSetConverter() }
    };

    // --- DataTable round-trips ---

    [Fact]
    public void DataTable_Reference_To_AsyncDataTable()
    {
        var dt = new DataTable("Users");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        dt.Rows.Add(1, "Alice");
        dt.Rows.Add(2, "Bob");
        dt.AcceptChanges();
        dt.Rows[1]["Name"] = "Robert";

        var json = JsonConvert.SerializeObject(dt, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.TableName.Should().Be("Users");
        result.Rows.Count.Should().Be(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Rows[1]["Name"].Should().Be("Robert");
        result.Rows[1]["Name", DataRowVersion.Original].Should().Be("Bob");
        result.Rows[1].RowState.Should().Be(DataRowState.Modified);
    }

    [Fact]
    public void AsyncDataTable_To_Reference_DataTable()
    {
        var table = new AsyncDataTable("Products");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Price", typeof(decimal));
        table.Rows.Add(1, 29.99m);
        table.AcceptChanges();

        var json = JsonConvert.SerializeObject(table, AsyncSettings());
        var result = JsonConvert.DeserializeObject<DataTable>(json, ReferenceSettings())!;

        result.TableName.Should().Be("Products");
        result.Rows.Count.Should().Be(1);
        result.Rows[0]["Price"].Should().Be(29.99m);
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public void All_RowStates_Round_Trip_Via_Reference()
    {
        var dt = new DataTable("States");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Val", typeof(string));
        dt.Rows.Add(1, "Unchanged");
        dt.Rows.Add(2, "WillModify");
        dt.Rows.Add(3, "WillDelete");
        dt.AcceptChanges();
        dt.Rows.Add(4, "Added");
        dt.Rows[1]["Val"] = "Modified";
        dt.Rows[2].Delete();

        var json = JsonConvert.SerializeObject(dt, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.Select("Id = 1")[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Select("Id = 2")[0].RowState.Should().Be(DataRowState.Modified);
        result.Select("Id = 2")[0]["Val"].Should().Be("Modified");
        result.Select("Id = 2")[0]["Val", DataRowVersion.Original].Should().Be("WillModify");
        result.Select("Id = 4")[0].RowState.Should().Be(DataRowState.Added);
        // Deleted rows survive in the collection
        var deletedRows = result.InnerDataTable.Select("Id = 3", null, DataViewRowState.Deleted);
        deletedRows.Should().HaveCount(1);
        deletedRows[0].RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public void Added_Row_Preserves_State_Round_Trip()
    {
        var dt = new DataTable("T");
        dt.Columns.Add("Id", typeof(int));
        dt.Rows.Add(1); // Added, AcceptChanges never called

        var json = JsonConvert.SerializeObject(dt, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.Rows[0].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void Proposed_Version_Serializes_Current_Values()
    {
        var dt = new DataTable("T");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        dt.Rows.Add(1, "Original");
        dt.AcceptChanges();
        dt.Rows[0].BeginEdit();
        dt.Rows[0]["Name"] = "Proposed";
        // EndEdit NOT called — row has Proposed version

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        // After deserialization, Proposed version is not reconstructible, row is Unchanged
        result.Rows[0]["Name"].Should().Be("Proposed");
    }

    [Fact]
    public void All_Primitive_Types_Round_Trip()
    {
        var dt = new DataTable("Types");
        dt.Columns.Add("Bool", typeof(bool));
        dt.Columns.Add("Int", typeof(int));
        dt.Columns.Add("Long", typeof(long));
        dt.Columns.Add("Float", typeof(float));
        dt.Columns.Add("Double", typeof(double));
        dt.Columns.Add("Decimal", typeof(decimal));
        dt.Columns.Add("String", typeof(string));
        dt.Columns.Add("DateTime", typeof(DateTime));
        dt.Columns.Add("Guid", typeof(Guid));
        dt.Columns.Add("Bytes", typeof(byte[]));

        var guid = Guid.NewGuid();
        var now = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        dt.Rows.Add(true, 42, 9999999999L, 3.14f, 2.718281828, 12345.6789012345678901234567m, "hello", now, guid, bytes);
        dt.AcceptChanges();

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;
        var row = result.Rows[0];

        row["Bool"].Should().Be(true);
        row["Int"].Should().Be(42);
        row["Long"].Should().Be(9999999999L);
        ((float)row["Float"]).Should().BeApproximately(3.14f, 0.001f);
        row["Decimal"].Should().Be(12345.6789012345678901234567m);
        row["String"].Should().Be("hello");
        row["Guid"].Should().Be(guid);
        ((byte[])row["Bytes"]).Should().Equal(bytes);
    }

    [Fact]
    public void Null_Values_Round_Trip()
    {
        var dt = new DataTable("Nulls");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string)) .AllowDBNull = true;
        dt.Rows.Add(1, DBNull.Value);
        dt.AcceptChanges();

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.Rows[0]["Name"].Should().Be(DBNull.Value);
    }

    [Fact]
    public void AutoIncrement_Column_Restores_Id()
    {
        var dt = new DataTable("AutoInc");
        var idCol = dt.Columns.Add("Id", typeof(int));
        idCol.AutoIncrement = true;
        idCol.AutoIncrementSeed = 100;
        idCol.AutoIncrementStep = 1;
        dt.Columns.Add("Name", typeof(string));
        dt.Rows.Add(null, "Alice"); // Id auto-assigned = 100
        dt.Rows.Add(null, "Bob");   // Id = 101
        dt.AcceptChanges();

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.Rows[0]["Id"].Should().Be(100);
        result.Rows[1]["Id"].Should().Be(101);
        result.Columns["Id"]!.AutoIncrement.Should().BeTrue();
    }

    [Fact]
    public void UniqueConstraint_And_PrimaryKey_Round_Trip()
    {
        var dt = new DataTable("T");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Code", typeof(string));
        dt.PrimaryKey = [dt.Columns["Id"]!];
        dt.Constraints.Add(new UniqueConstraint("UQ_Code", dt.Columns["Code"]!, isPrimaryKey: false));
        dt.Rows.Add(1, "A");
        dt.AcceptChanges();

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.PrimaryKey.Should().HaveCount(1);
        result.PrimaryKey[0].ColumnName.Should().Be("Id");
        result.Constraints.Cast<Constraint>().Should().ContainSingle(c => c.ConstraintName == "UQ_Code");
    }

    // --- DataSet round-trips ---

    [Fact]
    public void DataSet_Reference_To_AsyncDataSet()
    {
        var ds = new System.Data.DataSet("TestDS");
        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("OrderId", typeof(int));
        orders.Columns.Add("Total", typeof(decimal));
        orders.Rows.Add(1, 99.99m);
        orders.Rows.Add(2, 150.00m);
        ds.AcceptChanges();
        orders.Rows[1]["Total"] = 175.50m;

        var json = JsonConvert.SerializeObject(ds, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, AsyncSettings())!;

        result.DataSetName.Should().Be("TestDS");
        result.Tables["Orders"]!.Rows.Count.Should().Be(2);
        result.Tables["Orders"]!.Rows[1].RowState.Should().Be(DataRowState.Modified);
        ((decimal)result.Tables["Orders"]!.Rows[1]["Total"]).Should().Be(175.50m);
    }

    [Fact]
    public void AsyncDataSet_To_Reference_DataSet()
    {
        var table = new AsyncDataTable("Customers");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(1, "Alice");
        table.AcceptChanges();

        var asyncDs = new AsyncDataSet("MyDS");
        asyncDs.Tables.Add(table.InnerDataTable);

        var json = JsonConvert.SerializeObject(asyncDs, AsyncSettings());
        var result = JsonConvert.DeserializeObject<System.Data.DataSet>(json, ReferenceSettings())!;

        result.DataSetName.Should().Be("MyDS");
        result.Tables["Customers"]!.Rows[0]["Name"].Should().Be("Alice");
    }

    [Fact]
    public void DataSet_With_Relation_Round_Trips()
    {
        var ds = new System.Data.DataSet("Shop");
        var customers = ds.Tables.Add("Customers");
        customers.Columns.Add("Id", typeof(int));
        customers.PrimaryKey = [customers.Columns["Id"]!];
        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("Id", typeof(int));
        orders.Columns.Add("CustomerId", typeof(int));
        orders.PrimaryKey = [orders.Columns["Id"]!];
        ds.Relations.Add("CustOrders", customers.Columns["Id"]!, orders.Columns["CustomerId"]!);
        customers.Rows.Add(1);
        orders.Rows.Add(100, 1);
        ds.AcceptChanges();

        var json = JsonConvert.SerializeObject(ds, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, AsyncSettings())!;

        result.Relations.Count.Should().Be(1);
        result.Relations["CustOrders"]!.ParentTable.TableName.Should().Be("Customers");
        result.Relations["CustOrders"]!.ChildTable.TableName.Should().Be("Orders");
    }
}
```

**Step 2: Run tests**

```bash
dotnet test tests/System.Data.Async.Integration.Tests --filter "FullyQualifiedName~NewtonsoftJsonCrossCompatibilityTests"
```
Expected: All tests pass. If any fail, check the serialization fix in Task 2.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Integration.Tests/NewtonsoftJsonCrossCompatibilityTests.cs
git commit -m "test: add NewtonsoftJsonCrossCompatibilityTests"
```

---

### Task 11: Write `SystemTextJsonCrossCompatibilityTests`

**Files:**
- Create: `tests/System.Data.Async.Integration.Tests/SystemTextJsonCrossCompatibilityTests.cs`

**Background:** Proves that (a) STJ and Newtonsoft produce identical JSON strings, and (b) STJ converters produce/consume data correctly. Helper methods are shared with the Newtonsoft tests.

**Step 1: Write the tests**

```csharp
using System.Data.Async.Converters;
using System.Data.Async.Converters.SystemTextJson;
using System.Data.Async.DataSet;
using System.Text.Json;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using NJsonConvert = Newtonsoft.Json.JsonConvert;

namespace System.Data.Async.Integration.Tests;

public class SystemTextJsonCrossCompatibilityTests
{
    private static JsonSerializerSettings NewtonsoftAsyncSettings() => new JsonSerializerSettings
    {
        Converters = { new AsyncDataTableConverter(), new AsyncDataSetConverter() }
    };

    private static System.Text.Json.JsonSerializerOptions StjOptions() => new System.Text.Json.JsonSerializerOptions
    {
        Converters = { new AsyncDataTableJsonConverter(), new AsyncDataSetJsonConverter() }
    };

    // --- Wire format parity ---

    [Fact]
    public void STJ_And_Newtonsoft_Produce_Identical_Json_For_Simple_Table()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(1, "Alice");
        table.AcceptChanges();

        var newtonsoftJson = NJsonConvert.SerializeObject(table, NewtonsoftAsyncSettings());
        var stjJson = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());

        stjJson.Should().Be(newtonsoftJson);
    }

    [Fact]
    public void STJ_And_Newtonsoft_Produce_Identical_Json_For_All_Row_States()
    {
        var table = new AsyncDataTable("States");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Val", typeof(string));
        table.Rows.Add(1, "Unchanged");
        table.Rows.Add(2, "WillModify");
        table.Rows.Add(3, "WillDelete");
        table.AcceptChanges();
        table.Rows.Add(4, "Added");
        table.Rows[1]["Val"] = "Modified";
        table.Rows[2].Delete();

        var newtonsoftJson = NJsonConvert.SerializeObject(table, NewtonsoftAsyncSettings());
        var stjJson = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());

        stjJson.Should().Be(newtonsoftJson);
    }

    [Fact]
    public void STJ_And_Newtonsoft_Produce_Identical_Json_For_Decimal_And_Bytes()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("Data", typeof(byte[]));
        table.Rows.Add(12345.6789012345678901234567m, new byte[] { 10, 20, 30 });
        table.AcceptChanges();

        var newtonsoftJson = NJsonConvert.SerializeObject(table, NewtonsoftAsyncSettings());
        var stjJson = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());

        stjJson.Should().Be(newtonsoftJson);
    }

    // --- STJ round-trips ---

    [Fact]
    public void AsyncDataTable_Round_Trips_Via_STJ()
    {
        var table = new AsyncDataTable("Products");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Price", typeof(decimal));
        table.Rows.Add(1, 49.99m);
        table.Rows.Add(2, 99.99m);
        table.AcceptChanges();
        table.Rows[1]["Price"] = 89.99m;

        var json = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;

        result.TableName.Should().Be("Products");
        result.Rows.Count.Should().Be(2);
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Rows[1].RowState.Should().Be(DataRowState.Modified);
        ((decimal)result.Rows[1]["Price"]).Should().Be(89.99m);
        ((decimal)result.Rows[1]["Price", DataRowVersion.Original]).Should().Be(99.99m);
    }

    [Fact]
    public void All_Row_States_Round_Trip_Via_STJ()
    {
        var table = new AsyncDataTable("States");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Val", typeof(string));
        table.Rows.Add(1, "Unchanged");
        table.Rows.Add(2, "WillModify");
        table.Rows.Add(3, "WillDelete");
        table.AcceptChanges();
        table.Rows.Add(4, "Added");
        table.Rows[1]["Val"] = "Modified";
        table.Rows[2].Delete();

        var json = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;

        result.Select("Id = 1")[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Select("Id = 2")[0].RowState.Should().Be(DataRowState.Modified);
        result.Select("Id = 4")[0].RowState.Should().Be(DataRowState.Added);
        var deleted = result.InnerDataTable.Select("Id = 3", null, DataViewRowState.Deleted);
        deleted.Should().HaveCount(1);
        deleted[0].RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public void Added_Row_Preserves_State_Via_STJ()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Id", typeof(int));
        table.Rows.Add(1);

        var json = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;

        result.Rows[0].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void All_Primitive_Types_Round_Trip_Via_STJ()
    {
        var dt = new AsyncDataTable("Types");
        dt.Columns.Add("Bool", typeof(bool));
        dt.Columns.Add("Int", typeof(int));
        dt.Columns.Add("Long", typeof(long));
        dt.Columns.Add("Double", typeof(double));
        dt.Columns.Add("Decimal", typeof(decimal));
        dt.Columns.Add("String", typeof(string));
        dt.Columns.Add("Guid", typeof(Guid));
        dt.Columns.Add("Bytes", typeof(byte[]));

        var guid = Guid.NewGuid();
        var bytes = new byte[] { 1, 2, 3 };
        dt.Rows.Add(true, 42, 9999999999L, 2.718, 12345.6789m, "hello", guid, bytes);
        dt.AcceptChanges();

        var json = System.Text.Json.JsonSerializer.Serialize(dt, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;
        var row = result.Rows[0];

        row["Bool"].Should().Be(true);
        row["Int"].Should().Be(42);
        row["Long"].Should().Be(9999999999L);
        row["Decimal"].Should().Be(12345.6789m);
        row["String"].Should().Be("hello");
        row["Guid"].Should().Be(guid);
        ((byte[])row["Bytes"]).Should().Equal(bytes);
    }

    [Fact]
    public void Null_Values_Round_Trip_Via_STJ()
    {
        var table = new AsyncDataTable("Nulls");
        table.Columns.Add("Id", typeof(int));
        var nameCol = table.Columns.Add("Name", typeof(string));
        nameCol.AllowDBNull = true;
        table.Rows.Add(1, DBNull.Value);
        table.AcceptChanges();

        var json = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;

        result.Rows[0]["Name"].Should().Be(DBNull.Value);
    }

    [Fact]
    public void STJ_Json_Deserializes_With_Newtonsoft_And_Vice_Versa()
    {
        var table = new AsyncDataTable("CrossTest");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Value", typeof(string));
        table.Rows.Add(1, "Test");
        table.AcceptChanges();

        // Serialize with STJ, deserialize with Newtonsoft
        var stjJson = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var fromStj = NJsonConvert.DeserializeObject<AsyncDataTable>(stjJson, NewtonsoftAsyncSettings())!;
        fromStj.Rows[0]["Value"].Should().Be("Test");

        // Serialize with Newtonsoft, deserialize with STJ
        var nJson = NJsonConvert.SerializeObject(table, NewtonsoftAsyncSettings());
        var fromNewtonsoft = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(nJson, StjOptions())!;
        fromNewtonsoft.Rows[0]["Value"].Should().Be("Test");
    }

    [Fact]
    public void AsyncDataSet_Round_Trips_Via_STJ()
    {
        var ds = new System.Data.DataSet("MyDS");
        var customers = ds.Tables.Add("Customers");
        customers.Columns.Add("Id", typeof(int));
        customers.Columns.Add("Name", typeof(string));
        customers.PrimaryKey = [customers.Columns["Id"]!];
        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("Id", typeof(int));
        orders.Columns.Add("CustomerId", typeof(int));
        orders.PrimaryKey = [orders.Columns["Id"]!];
        ds.Relations.Add("CustOrders", customers.Columns["Id"]!, orders.Columns["CustomerId"]!);
        customers.Rows.Add(1, "Alice");
        orders.Rows.Add(100, 1);
        ds.AcceptChanges();

        var asyncDs = new AsyncDataSet(ds);
        var json = System.Text.Json.JsonSerializer.Serialize(asyncDs, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataSet>(json, StjOptions())!;

        result.DataSetName.Should().Be("MyDS");
        result.Tables["Customers"]!.Rows[0]["Name"].Should().Be("Alice");
        result.Tables["Orders"]!.Rows[0]["CustomerId"].Should().Be(1);
        result.Relations.Count.Should().Be(1);
        result.Relations["CustOrders"]!.ParentTable.TableName.Should().Be("Customers");
    }
}
```

**Step 2: Run tests**

```bash
dotnet test tests/System.Data.Async.Integration.Tests --filter "FullyQualifiedName~SystemTextJsonCrossCompatibilityTests"
```
Expected: All tests pass. If wire format parity tests fail, compare the JSON strings and align property ordering or value encoding in the STJ converter.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Integration.Tests/SystemTextJsonCrossCompatibilityTests.cs
git commit -m "test: add SystemTextJsonCrossCompatibilityTests"
```

---

### Task 12: Run full test suite and verify

**Step 1: Run everything**

```bash
dotnet test
```
Expected: All test projects pass. Pay attention to:
- `System.Data.Async.DataSet.Tests` — converter tests now reference new package
- `System.Data.Async.Integration.Tests` — all integration tests pass
- `System.Data.Async.Validation.Tests` — no regressions

**Step 2: Build entire solution in release mode**

```bash
dotnet build -c Release
```
Expected: Build succeeded, 0 warnings (TreatWarningsAsErrors is on).

**Step 3: Commit if any fixups were needed, then final commit**

```bash
git add .
git commit -m "chore: verify full test suite passes after serialization package extraction"
```

---

### Task 13: Update package descriptions and README

**Files:**
- Modify: `README.md`
- Modify: `src/System.Data.Async.DataSet/System.Data.Async.DataSet.csproj` (update description — Newtonsoft removed)

**Step 1: Update DataSet csproj description** (already done in Task 3 — confirm it no longer mentions JSON converters)

**Step 2: Update README.md**

In the Installation section, add the two new packages after `AdoNet.Async.DataSet`:

```bash
# Newtonsoft.Json converters (Json.Net.DataSetConverters wire format)
dotnet add package AdoNet.Async.Serialization.NewtonsoftJson

# System.Text.Json converters (same wire format)
dotnet add package AdoNet.Async.Serialization.SystemTextJson
```

In the Packages table, add two new rows:

```
| **AdoNet.Async.Serialization.NewtonsoftJson** | `AsyncDataTableConverter`, `AsyncDataSetConverter` for Newtonsoft.Json | AdoNet.Async.DataSet, Newtonsoft.Json |
| **AdoNet.Async.Serialization.SystemTextJson** | `AsyncDataTableJsonConverter`, `AsyncDataSetJsonConverter` for System.Text.Json | AdoNet.Async.DataSet |
```

Update the JSON serialization example section to show both converters and correct namespace (`System.Data.Async.Converters.SystemTextJson` for STJ).

**Step 3: Commit**

```bash
git add README.md src/System.Data.Async.DataSet/
git commit -m "docs: update README with new serialization packages"
```
