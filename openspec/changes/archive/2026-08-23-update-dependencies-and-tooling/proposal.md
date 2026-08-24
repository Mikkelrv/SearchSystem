## Why

The solution's NuGet packages are behind current releases, and one transitive dependency
(`SQLitePCLRaw.lib.e_sqlite3` 2.1.6, pulled in via `Microsoft.Data.Sqlite` 8.0.1) has a known
high-severity vulnerability (GHSA-2m69-gcr7-jv3q). Beyond the version bump, the repo currently
has no SDK pinning, no centralized package version management, and no editorconfig or CI - so
package versions are duplicated by hand across two projects and there's no automated build
check on push. This is housekeeping to get the repo current and reproducible before using it
as a base for studying architecture principles; no application code changes.

## What Changes

- Bump `Microsoft.Data.Sqlite` 8.0.1 → 10.0.11 and `Npgsql` 9.0.3 → 10.0.3 in `indexer` and
  `ConsoleSearch` (resolves the `SQLitePCLRaw` advisory transitively).
- Add `Directory.Packages.props` at the solution root enabling Central Package Management,
  and update the two `.csproj` files to reference package versions centrally instead of
  duplicating `Version="..."` per project.
- Add a `global.json` at the solution root pinning the .NET SDK version in use
  (`10.0.400`) for reproducible builds.
- Add a root `.editorconfig` encoding the C# naming conventions already documented in
  `openspec/config.yaml` (PascalCase types/methods/public members, camelCase locals/params,
  `_camelCase` private fields, `I`-prefixed interfaces), so the IDE/analyzers surface the
  same conventions the project already expects.
- Add a GitHub Actions workflow (`.github/workflows/build.yml`) that runs `dotnet restore`
  and `dotnet build` on push/PR against `main`, as a basic safety net for future changes.

No `.cs` files are modified by this change.

## Capabilities

### New Capabilities
(none - this is tooling/config only, no observable application behavior changes)

### Modified Capabilities
(none)

## Impact

- **Affected files**: `indexer/indexer.csproj`, `ConsoleSearch/ConsoleSearch.csproj`, new
  `Directory.Packages.props`, new `global.json`, new `.editorconfig`, new
  `.github/workflows/build.yml`.
- **Affected dependencies**: `Microsoft.Data.Sqlite`, `Npgsql`, transitively `SQLitePCLRaw.*`.
- **Not affected**: `Shared.csproj` (no package references), all `.cs` source files, runtime
  behavior of `indexer` and `ConsoleSearch`.
