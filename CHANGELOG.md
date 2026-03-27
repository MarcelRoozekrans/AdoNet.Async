# Changelog

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
