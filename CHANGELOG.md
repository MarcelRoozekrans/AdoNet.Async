# Changelog

## [1.3.0](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v1.2.1...v1.3.0) (2026-05-30)


### Features

* **aot:** declare AdoNet.Async + Adapters AOT-compatible ([#101](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/101)) ([aa350a0](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/aa350a0f8e997baf84c41334bfbff8f46ff2521c))
* **batch:** add IAsyncDbBatch / IAsyncDbBatchCommand ([#102](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/102)) ([9d61ec2](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/9d61ec2fd440d3476cd909cbbc63830a3ec6acd6))

## [1.2.1](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v1.2.0...v1.2.1) (2026-04-30)


### Bug Fixes

* **deps:** update docusaurus monorepo to v3.10.1 ([fbe970f](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/fbe970f52ce07000abe103a3c2cfdd3e8276f3ea))
* **deps:** update docusaurus monorepo to v3.10.1 ([76b7237](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/76b7237aac7a1fe57386dee661a5def487391db2))

## [1.2.0](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v1.1.5...v1.2.0) (2026-04-17)


### Features

* fill test and documentation coverage gaps ([#58](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/58)) ([98d1308](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/98d1308855b7b244f14755c91bbf01cf9194fa0e))


### Bug Fixes

* **deps:** update docusaurus monorepo to v3.10.0 ([#62](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/62)) ([dad2c20](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/dad2c202b8fada1e195efa43944bc6813b9ff896))
* pin webpack to 5.105.1 to avoid ProgressPlugin schema regression in 5.106.0 ([#60](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/60)) ([5d05a67](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/5d05a67fa59e7b2f5ada244b14b4125f2722f6fc))
* remove spurious leading comma in AddRowAsync when all columns are ReadOnly ([#63](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/63)) ([04602ef](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/04602ef95f7642968ed0eb14352f93c9f21f0788))

## [1.1.5](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v1.1.4...v1.1.5) (2026-04-17)


### Bug Fixes

* apply code review corrections from post-review audit ([41acb42](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/41acb42b3965798b23f75b1d41faa137f856325a))
* resolve top-level xs:complexType when table uses external type reference (fixes [#55](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/55)) ([3342277](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/33422773caccaac733b2d711f5ad441fe47aeae1))

## [1.1.4](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v1.1.3...v1.1.4) (2026-04-17)


### Bug Fixes

* make nullable columns optional parameters in AddRowAsync ([fe67561](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/fe6756148dbea08cd84e950ac27eed5a7c9684f4))
* parse xs:attribute columns in XSD parser (fixes [#52](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/52)) ([b445675](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/b445675b04d2c6fd6ab546a6206ff4239b6d5f8e))

## [Unreleased]

### Bug Fixes

* parse `xs:attribute` column declarations in XSD parser — tables using attribute-style columns (common in VS Dataset Designer schemas) now generate correct column field declarations, property accessors, `InitClass`, `InitVars`, and `AddRowAsync` parameter lists ([#52](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/52)). `use="required"` marks the column non-nullable; absent `use` or `use="optional"` marks it nullable — consistent with `minOccurs="0"` for `xs:element` columns. Mixed `xs:element` + `xs:attribute` columns in the same table are supported. All `codegen:` and `msdata:` column annotations work identically on both forms.
* strip leading `@` XPath prefix from `xs:field` references so attribute-column names in composite primary keys and foreign key constraints match the parsed column names

## [1.1.3](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v1.1.2...v1.1.3) (2026-04-17)


### Bug Fixes

* prefix generated per-table class names with dataset name to prevent collisions ([08d36ab](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/08d36ab9690933fd94aacfb63b775e07213107aa))
* strip XML namespace prefix from XPath-extracted identifiers in XSD parser ([1dea276](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/1dea2762627ea3c22f4c36d3327c8145ef867e0e))
* update integration tests and benchmarks to use dataset-prefixed class names ([d4034f1](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/d4034f140d496cb7ddb908eeb6e4a000d4ae9a1d))

## [1.1.1](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v1.1.0...v1.1.1) (2026-04-16)


### Bug Fixes

* brighten dark mode hero gradient further ([e89f397](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/e89f3978d8c1107d4ce05a20cfa62602154ad3f8))
* lighten light mode hero gradient ([e1ad25d](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/e1ad25da4d046c752379206afabfcafdf570ce81))
* prefix generated per-table class names with dataset name to prevent collisions ([08d36ab](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/08d36ab9690933fd94aacfb63b775e07213107aa))
* strip XML namespace prefix from XPath-extracted identifiers in XSD parser ([1dea276](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/1dea2762627ea3c22f4c36d3327c8145ef867e0e))
* update integration tests and benchmarks to use dataset-prefixed class names ([d4034f1](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/d4034f140d496cb7ddb908eeb6e4a000d4ae9a1d))

## [1.1.0](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v1.0.0...v1.1.0) (2026-03-29)


### Features

* add landing page with hero banner, features, code comparison, and social card ([34878df](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/34878df9b99696f300ba6ae6c3a348272d8fdbec))


### Bug Fixes

* lighten dark mode hero gradient for better visibility ([7ee2c61](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/7ee2c61458d90b5a8ff31305d98dd591355c627c))
* match hero gradient to logo color palette ([cab84b7](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/cab84b78154d5408030c9bf6f350285dda9a58e5))
* remove brightness filter from hero logo ([f4ccdfb](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/f4ccdfbe0c188b733b90c1974fb3e5dcf1d67d8d))
* set docs as root route and fix navbar logo link ([04653eb](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/04653eb17a83e2e17949b410f8dd18cfa442c675))
* update navbar and footer links for docs-as-root route ([f0f9d46](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/f0f9d468295684c1d5ce42f05eae0d7adf69cf28))
* use 'markup' instead of 'xml' for Prism language in Docusaurus ([4929755](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/492975545d0e6e78a3c3ee113167120c439dc32c))

## [1.0.0](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.5.0...v1.0.0) (2026-03-29)


### ⚠ BREAKING CHANGES

* wrap AsyncDataTable return types to prevent inner type leaks
* convert implicit DataTable/DataSet operators to explicit

### Features

* add AsyncDataTableCollection and seal AsyncDataSet return types ([cde5f34](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/cde5f34f966e2f0bdbca9df57b5f5b46c543d85c))
* add identity-preserving row cache to AsyncDataTable ([5ce0195](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/5ce01952239758996be9cf407e6dd24a2d6cddd3))
* AsyncDataTable.DataSet returns AsyncDataSet with parent back-reference ([f0b4d22](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/f0b4d2202fc7aa9c20368041a3d731cb1957faf5))
* scaffold Docusaurus site with placeholder pages and GitHub Pages workflow ([05c8b86](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/05c8b8669b4784b9bc88e16e951a5fe17fe8c56c))


### Bug Fixes

* **ci:** add Generator package to all workflows, use NuGetVersionV2 for prerelease ([3e642fe](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/3e642fe201e772c4fdbaa722d9c2c99fdac348bd))
* **ci:** fix GitVersion crash and docs build ([9b6a4e6](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/9b6a4e62ec9a96042c11ae923ea1c16f524cde04))
* **ci:** split CI into build-test and pack-push jobs ([6d5b2e4](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/6d5b2e4510d9542983ddc52f5965d91924006673))
* **ci:** use ContinuousDelivery mode for alpha prerelease versions ([b785c43](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/b785c43c5caac064f3fdf3f6b8330e94473c8820))
* **ci:** use SemVer for pack version (NuGetVersionV2 removed in GitVersion 6) ([4421b60](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/4421b6053fab8eccc43a9f9f15208f1e87b18a0d))


### Code Refactoring

* convert implicit DataTable/DataSet operators to explicit ([d8cd90f](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/d8cd90f0a4aa20229b9a765ea0dc5ae576027dd3))
* wrap AsyncDataTable return types to prevent inner type leaks ([6d289cc](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/6d289cc46e4fe4eb13c21375659330fd062dd399))

## [0.5.0](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.4.0...v0.5.0) (2026-03-28)


### Features

* add AsyncDataRowCollection&lt;TRow&gt; generic typed collection ([370ac57](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/370ac570e2348c16f20a8a73b444e00d206b6fda))
* add AsyncDataTable&lt;TRow&gt; generic typed table base class ([82ec058](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/82ec05844ee532906c4782679fe59fcb6f973664))
* add intermediate model types and XSD parser ([3826ac5](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/3826ac5b2e9f97a1f521c5e46ee50cda74c90384))
* add NamingHelper and code emitters for typed DataRow, DataTable, DataSet, and EventArgs ([2fe5681](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/2fe5681a6e9dfddbd11f9369b9fafebc8464b6bd))
* scaffold source generator and test projects ([25a96b8](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/25a96b8d5383708e7c128b0163c2d4503b28c699))
* typed DataSet source generator from .xsd files ([11cd1d9](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/11cd1d91a9ea805b5e397745771bc97f661c4b61))
* wire incremental generator pipeline with driver tests ([fa2bd75](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/fa2bd75cd17ce1fea8fa98454e20ee63715dc26d))


### Bug Fixes

* add missing System.Data.Common using to adapter test files ([316c65a](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/316c65a7f3974f2674d223c20be798783443cc77))
* correct emitter issues found during integration testing ([a285705](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/a2857055b9f4d74f987d899ea03a400744971df1))
* exclude expression and read-only columns from AddRowAsync parameters ([1576a19](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/1576a195aed051d63ea8c4bd5209c45b8f84b3ae))
* resolve null table in relation navigation and bypass of async events in AddRowAsync ([9fffc69](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/9fffc699b013870293470bd765f46ca6e9afdfee))
* use typedPlural for DataSet accessor names and typedName for parent row params ([7e56530](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/7e565305dc2998ed53fa236906c46e29f4bd05e7))


### Performance Improvements

* add typed vs untyped DataSet benchmarks ([274c2da](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/274c2da161bf67e5e3782535a7ed3bb847b6259c))

## [0.4.0](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.3.2...v0.4.0) (2026-03-27)


### Features

* add AsyncDataRow.AsyncTable property returning AsyncDataTable ([ad9d581](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/ad9d58143dbf6ecb4065d297c2f5e97acd1ff9e1))

## [0.3.2](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.3.1...v0.3.2) (2026-03-27)


### Bug Fixes

* publish serialization packages to NuGet ([7d42906](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/7d429066b0f54de172bc0ffbcadba73b700d90b3))

## [0.3.1](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.3.0...v0.3.1) (2026-03-27)


### Bug Fixes

* add bounds check to SetValueAsync(int columnIndex) on AsyncDataRow ([1a7ae58](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/1a7ae5897d1e87cb7adb9b6ece2381000e7085a9))

## [0.3.0](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.2.0...v0.3.0) (2026-03-27)


### Features

* add AsyncDataRow, AsyncDataRowCollection and async events to AsyncDataTable ([1c00a30](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/1c00a303793c7541a0e1f540e654aa9f290b2238))
* add ZeroAlloc.AsyncEvents dependency to DataSet package ([851c072](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/851c07289324ff0a07ffb17f7237eec302ed7425))
* guard sync-over-async bridge for WASM compatibility ([773dc24](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/773dc246294e780ef984045163055ae0969473ff))


### Bug Fixes

* migrate remaining sync row mutations to async API ([0cb17d9](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/0cb17d9bda9f347c10f4675d7fad0704c0539ad5))
* resolve code review issues in async events implementation ([216ffe8](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/216ffe86aa46c7d904581c52cffac39971afbffd))

## [0.2.0](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.1.4...v0.2.0) (2026-03-27)


### Features

* add AdoNet.Async.Serialization.NewtonsoftJson project with moved converters ([dab0663](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/dab066388135e51a8a58fdc43af6106070b6b361))
* add AdoNet.Async.Serialization.SystemTextJson with AsyncDataTableJsonConverter and AsyncDataSetJsonConverter ([d2d644c](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/d2d644c4e7000b41a0db8ad5050559375dc99f61))


### Bug Fixes

* add missing numeric types to STJ WriteColumnValue; pass options through ReadTables; deduplicate WriteExtendedProperties ([e63e0c3](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/e63e0c3a4609b8be7ddac1e88ffe60f9c18bee1e))
* deserialize Detached RowState as Added; add Proposed and Detached tests ([6cb3d24](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/6cb3d2435d528108f3213dfa699d08dd650b1d18))
* handle DataRowVersion.Proposed and Detached rows in Newtonsoft converter ([9781b65](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/9781b653342a97b2ccdb76a6c85f522046c81f25))

## [0.1.4](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.1.3...v0.1.4) (2026-03-22)


### Bug Fixes

* correct dotnet-version format (10.0.x not net10.0) ([f0cf3d2](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/f0cf3d20cc223a4cc54e68d2e97369e5d97318ad))

## [0.1.3](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.1.2...v0.1.3) (2026-03-22)


### Features

* make AsyncDataTable and AsyncDataSet wrapping constructors and Inner properties public ([2485260](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/24852602530d57ab0e834c92964d3042dcfaf5b9))

## [0.1.2](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.1.1...v0.1.2) (2026-03-22)


### Features

* add logo and wire into NuGet packages ([acaf3a3](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/acaf3a39cf1b996d97c29fb79a585b32aceef17a))

## [0.1.1](https://github.com/MarcelRoozekrans/AdoNet.Async/compare/v0.1.0...v0.1.1) (2026-03-22)


### Features

* add abstract base classes (AsyncDbDataReader, AsyncDbTransaction, AsyncDbCommand, AsyncDbConnection) ([0813dd7](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/0813dd7f427e546a36de55c9c8e4ed5cbd0febe1))
* add adapter package (AdapterDbConnection, AdapterDbCommand, AdapterDbDataReader, AdapterDbTransaction, extensions) ([57ad1be](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/57ad1bed323b79bf769c9f8a3f7e2ddd13dc450b))
* add async XML I/O methods to AsyncDataTable and AsyncDataSet ([dfc2693](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/dfc2693fc6a99697d0829f6f6c3f5595d8324728))
* add AsyncDataTable, AsyncDataSet, AsyncDataAdapter with FillAsync ([df4bda6](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/df4bda65c91d054754c17043f7d6b111bfcd84d9))
* add benchmark project with 5 benchmark classes and custom parity exporter ([9140717](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/91407174366ffc5d4ea7d9c83c5750f012106753))
* add BenchmarkDotNet project scaffold with shared benchmark infrastructure ([f2255f4](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/f2255f49d756d665187b92f0e21869a55c0f4c38))
* add core async interfaces (IAsyncDataReader, IAsyncDbTransaction, IAsyncDbCommand, IAsyncDbConnection, IAsyncDbProviderFactory) ([e61ad94](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/e61ad94d60675ea9165bc88023aee3a14232ba28))
* add cross-compatibility tests with Json.Net.DataSetConverters ([91900f6](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/91900f6d7e2ed5f355107be3e6b1ed3e3900e040))
* add end-to-end integration tests ([736fad4](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/736fad4c7c2f2ed1eb4621a12791b56ceb7312a9))
* add JSON converters compatible with Json.Net.DataSetConverters format ([8718adb](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/8718adb0b16545848f6e7a821b35ad07a156c812))
* add Validation.Tests project with SQLite test infrastructure ([d2c08cd](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/d2c08cdb749dcc6e1816949a0439b80311ad395d))
* implement UpdateAsync in AdapterDbDataAdapter ([017c476](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/017c47656a0937590439659a22b563da430d700a))
* rename NuGet packages to AdoNet.Async (namespace unchanged) ([18a8668](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/18a866842854a8e1764464936d9e9830d61307e2))
* scaffold solution with three packages and test projects ([20a216a](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/20a216a459119ab6efba9c59ccc0781718d55748))


### Bug Fixes

* proper inner disposal in adapters, honest async XML docs, sync bridge consistency ([a59e3b8](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/a59e3b85369495dbc0f9eb51b296ffb546cad735))
* resolve all analyzer warnings from new analyzers, rerun benchmarks ([a5ff0cc](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/a5ff0ccaaaaaaf2b4fc0d12387b4cb23f2c304f8))


### Documentation

* add benchmark results to README, expand validation coverage to 40 tests ([db7cb7e](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/db7cb7efa90ff82e62954cd7547b5d5a61eed998))
* add README with installation and usage examples ([7493cbf](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/7493cbf52814e430d2be3a5b080dc3e06365dac6))


### Tests

* add Connection, Command, Reader, and Transaction parity tests ([df53634](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/df5363424c112c71a16aee32e6d37571930e5543))
* add DataAdapter, Serialization, Event, and EdgeCase parity tests ([e0bef87](https://github.com/MarcelRoozekrans/AdoNet.Async/commit/e0bef87baae5496e188bcdfc5d250b297ca91b04))
