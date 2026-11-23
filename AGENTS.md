# AGENTS.md: AdoNet.Async

Async-first interfaces and base classes for ADO.NET, published as NuGet packages. The folders and
namespaces are `System.Data.Async.*`, but the package ids are `AdoNet.Async.*`. Six packages ship
from `src/`: the core interfaces, `DataSet`, the two `Serialization.*` converters, `Adapters` (wrappers
for existing providers plus DI) and the `DataSet.Generator` source generator.

## Layout

- Everything under `tests/` is unpublished. `AotSmoke` and `Benchmarks` are executables, not test projects.
- `src/System.Data.Async.DataSet.Generator` targets `netstandard2.0` with `LangVersion` `latest`, because
  it runs inside the compiler. It is packed as an analyzer and not referenced as an assembly.
- Every other project inherits `net10.0;net11.0`, `LangVersion` `preview` and warnings as errors from
  `Directory.Build.props`.
- `PendingProvider.cs` in the Adapters tests is compiled into the Benchmarks project by a linked
  `Compile` item. Moving or renaming it breaks the benchmarks.
- `website/` is the Docusaurus site. `docs/plans/` holds historical design notes, not current docs.

## Build and test

Prerequisites: `global.json` pins SDK `11.0.100` with `rollForward: latestMinor`, so an SDK 10 alone
fails. It is the only `global.json`. Test projects target `net10.0` and `net11.0`, so both the .NET 10
and .NET 11 runtimes must be installed. CI installs `10.0.x` and `11.0.x`.

CI job `build-test` in `.github/workflows/ci.yml`:

```bash
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release --verbosity normal --collect "XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults
```

The same job then runs the `coverage-gate` action from `MarcelRoozekrans/ci-baseline` on
`TestResults/**/coverage.cobertura.xml`. It fails below 80 percent line coverage. The commands above
produce the reports, but running the gate itself needs that action, so it is not reproducible locally.

Fast inner loop, verified locally:

```bash
dotnet test tests/System.Data.Async.Tests -c Release -f net10.0 --filter "FullyQualifiedName~Async"
```

CI job `aot-publish-smoke` in `.github/workflows/aot-smoke.yml`:

```bash
dotnet restore tests/System.Data.Async.AotSmoke/System.Data.Async.AotSmoke.csproj
dotnet publish tests/System.Data.Async.AotSmoke/System.Data.Async.AotSmoke.csproj -c Release -r linux-x64 --self-contained true -p:PublishAot=true -o ./aot-smoke-out
./aot-smoke-out/System.Data.Async.AotSmoke
```

`AotSmoke` targets `net11.0` only and needs the native AOT toolchain (clang and zlib headers on Linux).

CI job `full` in `.github/workflows/benchmarks.yml` is manual, and `smoke` runs the shared
ZeroAlloc `benchmark-smoke.yml` on every PR:

```bash
dotnet build tests/System.Data.Async.Benchmarks -c Release
dotnet run --project tests/System.Data.Async.Benchmarks -c Release --no-build -- --filter "${FILTER:-*}"
```

Other CI jobs: `baseline-checks.yml` calls the shared `ci-baseline` workflow, and `docs.yml` builds
`website/` with Node 26 when files under it change.

## Conventions and gotchas

- `Benchmarks` targets `net10.0` only, so it needs only the .NET 10 runtime.
- `Directory.Build.props` references the analyzers Meziantou, Roslynator, ErrorProne.NET, Hyperlinq and
  ZeroAlloc.Analyzers with `TreatWarningsAsErrors`. A new warning fails the build; fix the code rather
  than suppressing it.
- `System.Data.Async`, `DataSet`, `Adapters` and both `Serialization.*` projects set
  `IsAotCompatible`. Reflection added there shows up as an IL2xxx or IL3xxx error. The `AotSmoke`
  publish references only `System.Data.Async` and `Adapters`, so it is the consumer-side proof for
  those two; it does not exercise `DataSet` or either `Serialization.*` converter.
- Adapters must stay reflection-free: the `DbProviderFactory` is passed as an instance and never looked
  up through `DbProviderFactories`.
- The generator references `Microsoft.CodeAnalysis.CSharp` 5.0.0, the compiler of SDK band 10.0.1xx, the
  oldest supported SDK. A compiler older than the Roslyn the DLL references skips the generator with
  `CS9057`, and nothing is generated. `renovate.json` holds it below 5.1; the test projects may use a newer
  Roslyn. If a newer Roslyn API is needed, change the code; raise the floor only with the minimum SDK.
- A new generator diagnostic goes in `Diagnostics.cs` and in
  `src/System.Data.Async.DataSet.Generator/AnalyzerReleases.Unshipped.md`. The generator project
  references `Microsoft.CodeAnalysis.Analyzers`, which reports a rule missing from that file as an error.
- The tests that exercise generated code take their `.xsd` files from `Schemas/` as `AdditionalFiles`.
  `Benchmarks` also reads `OrdersDS.xsd` from the Generator.Integration.Tests `Schemas` folder.
- The Serialization converters are wire-compatible with `Json.Net.DataSetConverters`. The
  `DataSet.Tests` and `Integration.Tests` projects compare against it, so a format change fails there.
- `SQLitePCLRaw.lib.e_sqlite3` is pinned in each project that uses `Microsoft.Data.Sqlite`, to override
  a vulnerable transitive native binary. Keep the pin in every one: `Adapters.Tests`,
  `Validation.Tests`, `Benchmarks` and `AotSmoke`.

<!-- org-core:start sha256=08a88996bc9165f888fc0260607cfd0670cae128c323e16707c6b452ee66222d (generated from docker-nuc scripts/agents-md/org-core-personal.md; do not edit here) -->
## Org rules

These rules hold in every public MarcelRoozekrans repo.

### Commits and releases

- Conventional Commits. release-please reads them on the default branch, and the changelog sections
  in `release-please-config.json` decide which types count. The standard: `feat`, `fix`, `perf`,
  `deps` and `revert` open or update the release PR; `docs`, `test`, `refactor`, `build`, `ci`,
  `chore` and `style` do not, unless the commit is breaking or has a `Release-As:` footer. The
  release happens when that PR merges. A breaking change needs `!` or a `BREAKING CHANGE:` footer.
- A commit body never contains nested parentheses. release-please cannot parse them and silently
  drops the whole commit from the changelog, while the workflow still reports success.
- After merging a commit that should release, check that it appears in the release-please PR.

### Branches and pull requests

- Every change goes through a branch and a pull request. Never push to the default branch directly.
- A PR body says what the diff does, and nothing the diff does not do.
- Fix review findings in the PR under review, including small ones. File an issue only for a finding
  that belongs to other code, needs a design decision, is its own piece of work, or waits on
  something external.

### CI

- The .NET repos call the shared workflows of `MarcelRoozekrans/ci-baseline`, pinned to `@v1`, never
  `@main`.
- Its coverage gate fails the build when line coverage drops below the floor of 80%, and Codecov
  fails a PR that lowers project coverage by more than 1 percentage point. New code comes with
  tests.
- A repo with a lower floor documents the reason and links the issue that raises it.
<!-- org-core:end -->
