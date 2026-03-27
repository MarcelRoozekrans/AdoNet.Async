# Integration Tests + Serialization Packages — Design Document

**Date:** 2026-03-27
**Status:** Approved

## Overview

Three deliverables:

1. Extract Newtonsoft.Json converters from `AdoNet.Async.DataSet` into a dedicated `AdoNet.Async.Serialization.NewtonsoftJson` package and fix serialization gaps against the reference implementation (`Json.Net.DataSetConverters`).
2. Add a new `AdoNet.Async.Serialization.SystemTextJson` package implementing the same wire format using `System.Text.Json`.
3. Add a new `System.Data.Async.Integration.Tests` project with in-memory interop and cross-serialization tests.

## Goals

- Prove `DataTable` ↔ `AsyncDataTable` and `DataSet` ↔ `AsyncDataSet` conversion is lossless
- Prove full serialization parity between our converters and `Json.Net.DataSetConverters`
- Prove STJ and Newtonsoft converters produce identical JSON for the same input
- Cover all `DataRowState` values including Proposed version and Detached rows
- Keep `AdoNet.Async.DataSet` dependency-free from JSON libraries

## Non-Goals

- No XML serialization package (XML stays in `AdoNet.Async.DataSet` using the BCL)
- No database-backed integration tests (already covered by the validation suite)
- No POCO mapping or query builder

## New Source Packages

### `AdoNet.Async.Serialization.NewtonsoftJson`

**Path:** `src/System.Data.Async.Serialization.NewtonsoftJson/`
**NuGet ID:** `AdoNet.Async.Serialization.NewtonsoftJson`
**Dependencies:** `AdoNet.Async.DataSet`, `Newtonsoft.Json 13.*`

Contains:
- `AsyncDataTableConverter` (moved from `System.Data.Async.DataSet/Converters/`)
- `AsyncDataSetConverter` (moved from `System.Data.Async.DataSet/Converters/`)
- Namespace stays `System.Data.Async.Converters` — no breaking change for consumers

`System.Data.Async.DataSet.csproj` drops its `Newtonsoft.Json` reference and the `Converters/` folder.

Serialization fixes required to match `Json.Net.DataSetConverters`:
- Use `DataRowVersion.Proposed` when available (row in `BeginEdit`) as current values
- Write ReadOnly columns while row is still detached during deserialization (restores AutoIncrement IDs)
- Serialize `DataRowState.Detached` rows with `OriginalRow: null`; on deserialization they become `Added` (documented, matching reference behavior)
- Align `RowState` wire format with reference (verify string vs integer encoding)

### `AdoNet.Async.Serialization.SystemTextJson`

**Path:** `src/System.Data.Async.Serialization.SystemTextJson/`
**NuGet ID:** `AdoNet.Async.Serialization.SystemTextJson`
**Dependencies:** `AdoNet.Async.DataSet` only (STJ is in-box on .NET 10)

Contains:
- `AsyncDataTableJsonConverter : JsonConverter<AsyncDataTable>`
- `AsyncDataSetJsonConverter : JsonConverter<AsyncDataSet>`
- Namespace: `System.Data.Async.Converters.SystemTextJson`

Must produce and consume the **exact same wire format** as the Newtonsoft converters, including:
- Same property names and ordering
- Same row state encoding
- Same `decimal` handling (`"F28"` string format)
- Same `byte[]` handling (Base64)
- Same `OriginalRow` structure for Modified/Deleted rows
- Same `DataRowVersion.Proposed` logic
- Same detached row handling

## Wire Format Reference

Based on `Json.Net.DataSetConverters`. Each row object:

```json
{
  "OriginalRow": { "<Col>": value, ..., "RowState": "Modified" } | null,
  "<Col1>": value,
  "<Col2>": value,
  "RowState": "Unchanged" | "Added" | "Modified" | "Deleted" | "Detached"
}
```

| RowState | OriginalRow | Current values from |
|---|---|---|
| `Unchanged` | null | Current |
| `Added` | null | Current |
| `Modified` | Original version | Current (or Proposed if in BeginEdit) |
| `Deleted` | Original version | Original (repeated) |
| `Detached` | null | Current |

`Detached` rows deserialize as `Added` — this is a known, documented limitation matching the reference implementation.

DataTable property order: `CaseSensitive`, `DisplayExpression`, `Locale`, `MinimumCapacity`, `Namespace`, `Prefix`, `RemotingFormat`, `TableName`, `Columns`, `Constraints`, `Rows`.

DataSet tables serialized as a JSON **object** keyed by `TableName` (not an array). Relations serialized with inline `ChildKeyConstraint` (ForeignKeyConstraints are NOT in the table's `Constraints` array).

## Integration Test Project

**Path:** `tests/System.Data.Async.Integration.Tests/`
**Dependencies:** `AdoNet.Async.DataSet`, `AdoNet.Async.Serialization.NewtonsoftJson`, `AdoNet.Async.Serialization.SystemTextJson`, `Json.Net.DataSetConverters`, xUnit, FluentAssertions

### Test Classes

#### `DataTableInteropTests`
Proves `DataTable` ↔ `AsyncDataTable` wrapping is lossless (in-memory, no serialization):
- Wrap a `DataTable` in `AsyncDataTable(DataTable inner)` — all rows, columns, constraints preserved
- Extract `InnerDataTable` from `AsyncDataTable` — equals source
- All row states: Unchanged, Added, Modified, Deleted
- Relations, UniqueConstraints, PrimaryKey
- Extended properties

#### `DataSetInteropTests`
Same coverage for `DataSet` ↔ `AsyncDataSet`:
- Multi-table sets with relations survive wrapping/unwrapping
- Constraints (UniqueConstraint, ForeignKeyConstraint) preserved
- `EnforceConstraints`, `CaseSensitive`, `Locale` preserved

#### `NewtonsoftJsonCrossCompatibilityTests`
Proves Newtonsoft converters are wire-compatible with `Json.Net.DataSetConverters`:
- `DataTable` → JSON (Json.Net.DataSetConverters) → `AsyncDataTable` (our converter) — round-trip
- `AsyncDataTable` → JSON (our converter) → `DataTable` (Json.Net.DataSetConverters) — round-trip
- Same for `DataSet` ↔ `AsyncDataSet`
- All row states: Unchanged, Added, Modified, Deleted, Detached (asserting Detached → Added)
- Row in `BeginEdit` (Proposed version) serializes correctly
- All primitive types: `int`, `long`, `string`, `bool`, `decimal`, `double`, `float`, `DateTime`, `DateTimeOffset`, `Guid`, `TimeSpan`, `byte[]`
- Nullable columns with DBNull values
- AutoIncrement columns (ReadOnly) restored correctly
- UniqueConstraints and PrimaryKey preserved
- Extended properties on table, column, and constraint

#### `SystemTextJsonCrossCompatibilityTests`
Proves STJ converters produce identical output and are wire-compatible:
- STJ and Newtonsoft produce **identical JSON strings** for the same `AsyncDataTable`/`AsyncDataSet`
- `DataTable` → Newtonsoft JSON → deserialize with STJ → verify data
- `DataTable` → STJ JSON → deserialize with Newtonsoft → verify data
- Full row state coverage (same matrix as Newtonsoft tests)
- All primitive types (same matrix)
- `AsyncDataSet` with relations round-trips via STJ

## Design Decisions

| Decision | Rationale |
|---|---|
| Move converters out of DataSet | Single responsibility; DataSet should have no JSON dependency |
| Match `Json.Net.DataSetConverters` wire format exactly | Interoperability guarantee — any JSON produced by either library can be consumed by the other |
| STJ separate package | Consumers choose their JSON library; no forced dependency |
| Detached → Added on deserialization | Matches reference implementation; `Detached` is transient state by definition |
| In-memory tests only | DB behavior already proven by validation suite; integration tests focus on type conversion and serialization |
| Existing `CrossCompatibilityTests.cs` subsumed | The new integration project covers all existing cases plus significantly more; old file removed |
