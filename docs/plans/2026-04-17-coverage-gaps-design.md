# Design: Fill Test & Documentation Coverage Gaps

**Date:** 2026-04-17  
**Status:** Approved  
**Branch:** `feat/coverage-gaps`

---

## Overview

The project is feature-complete at v1.1.4 but has identified gaps in test coverage and Docusaurus documentation for several delegated ADO.NET features. This document describes what will be added and how.

---

## Scope

### Tests (new test class in `System.Data.Async.DataSet.Tests`)

| Feature | What to test |
|---------|-------------|
| `Merge()` | `MissingSchemaAction.Add`, `MissingSchemaAction.Ignore`, `MissingSchemaAction.Error`; `preserveChanges=true` vs `false`; merge into empty table; merge with overlapping rows |
| `Compute()` | Aggregate expressions (`Sum`, `Count`, `Avg`, `Min`, `Max`) on a plain `AsyncDataTable`; filter expressions; `DBNull` handling |
| `LoadDataRow()` | `LoadOption.OverwriteChanges`, `LoadOption.PreserveChanges`, `LoadOption.Upsert`; bool overload (accept=true/false) |
| `BeginInit/EndInit` | Call sequence does not throw; columns added between Begin/End are visible after EndInit |
| `BeginLoadData/EndLoadData` | Row events are suppressed during load; constraints are re-evaluated after EndLoadData |
| `Reset()` | Table is empty after Reset; columns are cleared |

All tests go in a new file `tests/System.Data.Async.DataSet.Tests/AsyncDataTableAdvancedTests.cs`.

### Docusaurus Docs (new pages in `website/docs/cookbook/`)

| File | Content |
|------|---------|
| `xml-io.md` | Async `WriteXmlAsync` / `ReadXmlAsync` round-trip; `WriteXmlSchemaAsync`; reading from a file path; streaming patterns |
| `merge-and-row-versioning.md` | `Merge()` with each `MissingSchemaAction`; `preserveChanges`; reading `DataRowVersion.Original` vs `Current` vs `Proposed` via `AsyncDataRow[name, version]`; `GetChanges()` workflow |
| `compute-constraints-relations.md` | `Compute()` aggregate expressions on a plain table; adding `UniqueConstraint` and `ForeignKeyConstraint` manually; adding `DataRelation` and navigating parent/child rows |

All pages follow the existing cookbook format: frontmatter with `sidebar_position` and `title`, H2 sections per scenario, C# code blocks, brief prose.

---

## Architecture

No new source code. All changes are:
- One new test file (tests only, no production code changes)
- Three new Markdown pages (docs only)

No API changes, no breaking changes, no NuGet version bump required.

---

## Testing Strategy

- New tests use the existing `xunit` + `FluentAssertions` stack
- Tests are synchronous where the feature is synchronous (Merge, Compute, LoadDataRow) — async `AddAsync`/`AcceptChangesAsync` used for setup only
- All existing 228 + 109 tests must continue to pass

---

## Delivery

Single branch `feat/coverage-gaps`, single PR to `main`.
