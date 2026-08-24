## Why

The repo has drifted behind its own toolchain: NuGet packages are stale (one with a known high-severity vulnerability), there's no `.editorconfig` to enforce the coding conventions already declared in `openspec/config.yaml`, package versions are hand-duplicated across two `.csproj` files, and nothing builds the solution automatically on push. None of this changes application behavior, but all of it makes the codebase harder to keep correct and consistent going forward.

## What Changes

- Upgrade `Microsoft.Data.Sqlite` from 8.0.1 to 10.0.11 (resolves the NU1903 high-severity `SQLitePCLRaw.lib.e_sqlite3` advisory) in `indexer` and `ConsoleSearch`.
- Upgrade `Npgsql` from 9.0.3 to 10.0.3 in `indexer` and `ConsoleSearch`.
- Add a `Directory.Packages.props` (central package management) so package versions are declared once instead of duplicated in `indexer.csproj` and `ConsoleSearch.csproj`.
- Add a repo-root `.editorconfig` encoding the naming/style conventions already declared in `openspec/config.yaml` (PascalCase/camelCase rules, `var` usage, etc.) so they're enforced by tooling, not just documented.
- Enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in all three `.csproj` files, matching current .NET SDK project defaults.
- Add a GitHub Actions workflow (`.github/workflows/build.yml`) that restores and builds `SearchSystem.sln` on push/PR, so a broken build is caught automatically.

## Capabilities

### New Capabilities
(none - pure tooling/dependency change, no spec-level behavior change)

### Modified Capabilities
(none)

This change sets `skip_specs: true` in its `.openspec.yaml`: nothing here is externally observable application behavior.

## Impact

- `indexer/indexer.csproj`, `ConsoleSearch/ConsoleSearch.csproj`, `Shared/Shared.csproj`: package version bumps, `Nullable`/`ImplicitUsings`, and switch to centrally-managed package versions.
- New: `Directory.Packages.props` (repo root).
- New: `.editorconfig` (repo root).
- New: `.github/workflows/build.yml`.
- Enabling `Nullable` may surface new compiler warnings in existing code; those are addressed as part of this change so the build stays warning-clean, not deferred.
- No changes to runtime behavior, public APIs, or the database layer's SQL/queries.
