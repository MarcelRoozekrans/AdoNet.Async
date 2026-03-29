# Docusaurus Documentation Site Design

## Problem

The library has no user-facing documentation beyond the README. Users need comprehensive guides, concept explanations, cookbook examples, and API reference to adopt the library effectively.

## Solution

A Docusaurus site in `website/` deployed to GitHub Pages via GitHub Actions. 23 documentation pages covering getting started, core concepts, typed DataSets, serialization, adapters/DI, cookbook recipes, API reference, and performance.

## Structure

```
website/
├── docusaurus.config.js
├── package.json
├── sidebars.js
├── static/img/logo.svg
├── src/css/custom.css
└── docs/
    ├── intro.md
    ├── getting-started/
    │   ├── installation.md
    │   └── quick-start.md
    ├── core-concepts/
    │   ├── async-interfaces.md
    │   ├── await-foreach.md
    │   ├── async-datatable.md
    │   ├── async-events.md
    │   └── sync-bridge.md
    ├── typed-datasets/
    │   ├── overview.md
    │   ├── generating-from-xsd.md
    │   ├── typed-access.md
    │   ├── relations.md
    │   └── annotations-reference.md
    ├── serialization/
    │   ├── newtonsoft-json.md
    │   └── system-text-json.md
    ├── adapters-di/
    │   ├── wrapping-providers.md
    │   └── dependency-injection.md
    ├── cookbook/
    │   ├── migrate-existing-code.md
    │   ├── fill-typed-dataset.md
    │   ├── async-validation-events.md
    │   └── json-api-typed-datasets.md
    ├── api-reference/
    │   └── api-reference.md
    └── performance/
        └── performance.md
```

## Deployment

GitHub Actions workflow `.github/workflows/docs.yml`:
- Triggers on pushes to `main` that touch `website/**`
- Builds with Node.js + `npm run build`
- Deploys to `gh-pages` branch
- Site URL: `https://marcelroozekrans.github.io/AdoNet.Async/`

## Post-Deployment

- Add docs URL to `Directory.Build.props` as `<PackageProjectUrl>`
- Add docs badge and link to README.md

## Page Summary (23 pages)

### Getting Started (3)
- intro.md — what & why
- installation.md — all 6 packages
- quick-start.md — .AsAsync() → await foreach

### Core Concepts (5)
- async-interfaces.md — IAsyncDbConnection, IAsyncDbCommand, IAsyncDataReader, etc.
- await-foreach.md — IAsyncEnumerable streaming
- async-datatable.md — AsyncDataTable, AsyncDataRow, mutations
- async-events.md — 9 events, ZeroAlloc.AsyncEvents
- sync-bridge.md — sync methods, WASM safety

### Typed DataSets (5)
- overview.md — generator output, before/after
- generating-from-xsd.md — .csproj setup, AdditionalFiles
- typed-access.md — typed properties, FindBy, AddRowAsync
- relations.md — parent/child navigation, FK-aware adds
- annotations-reference.md — codegen:* and msdata:* table

### Serialization (2)
- newtonsoft-json.md — setup, round-trip
- system-text-json.md — STJ setup, wire compatibility

### Adapters & DI (2)
- wrapping-providers.md — .AsAsync(), adapter classes, FillAsync
- dependency-injection.md — AddAsyncData(), IAsyncDbProviderFactory

### Cookbook (4)
- migrate-existing-code.md — step-by-step migration
- fill-typed-dataset.md — .xsd → fill → read typed rows
- async-validation-events.md — RowChangingAsync validation
- json-api-typed-datasets.md — ASP.NET Core JSON API

### API Reference (1)
- api-reference.md — per-package type listing

### Performance (1)
- performance.md — benchmarks, design decisions, ValueTask rationale
