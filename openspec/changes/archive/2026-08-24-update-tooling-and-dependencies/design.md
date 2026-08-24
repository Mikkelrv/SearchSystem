## Context

Three `.csproj` files (`Shared`, `indexer`, `ConsoleSearch`) all target `net10.0`. Package versions for `Microsoft.Data.Sqlite` and `Npgsql` are currently repeated identically in `indexer.csproj` and `ConsoleSearch.csproj`. There is no `.editorconfig`, no `Nullable`/`ImplicitUsings` opt-in, and no CI. The repo's GitHub remote is `github.com/Mikkelrv/SearchSystem`. See proposal.md - Why / What Changes for motivation and scope.

## Goals / Non-Goals

**Goals:**
- Get the solution to build clean, with no known-vulnerable transitive packages and no compiler warnings, after enabling `Nullable`.
- Enforce the naming/style conventions already declared in `openspec/config.yaml` via `.editorconfig` rather than only in prose.
- Make CI catch a broken build automatically on every push/PR.
- Remove the duplicated package-version declarations between `indexer.csproj` and `ConsoleSearch.csproj`.

**Non-Goals:**
- Adding a test project or test suite (there are none today; introducing tests is a separate, larger change).
- Adding static analyzers/analysis rulesets beyond what `Nullable` and the SDK's built-in warnings surface - a dedicated analyzer package (e.g. `Microsoft.CodeAnalysis.NetAnalyzers` tightening, StyleCop) is future work, not bundled here.
- Changing runtime/application behavior, database schema, or public APIs.
- Adding CD/deployment/publish steps - this change only adds build verification.

## Decisions

**Central package management via `Directory.Packages.props`** over hand-syncing versions in each `.csproj`.
Alternative considered: leave versions duplicated and just bump both files in lockstep. Rejected - it's exactly the kind of DRY violation `openspec/config.yaml` already flags, and it's how the two projects drifted out of sync with best-practice versions in the first place. Central package management is the standard .NET SDK mechanism (`ManagePackageVersionsCentrally`) so it needs no extra tooling.

**Bump `Microsoft.Data.Sqlite` to 10.0.11 and `Npgsql` to 10.0.3 (latest stable at time of writing)** rather than the minimum version that clears the NU1903 advisory.
Alternative considered: bump only just enough to drop the vulnerable transitive `SQLitePCLRaw.lib.e_sqlite3` version. Rejected - the project already targets `net10.0`; pinning to the matching major-version-aligned package release avoids next revisiting this the moment another advisory lands on an already-stale 8.x line.

**`.editorconfig` mirrors `openspec/config.yaml`'s conventions section, not a broader style guide.**
Alternative considered: adopt a full community `.editorconfig` template (e.g. dotnet/roslyn's). Rejected for this change - scope is to make the already-agreed conventions enforceable, not to introduce new unagreed ones. A fuller template is a candidate for a future change.

**Enable `Nullable` and `ImplicitUsings` per-project in each `.csproj`** (not centrally in `Directory.Build.props`) to keep this slice minimal.
Alternative considered: also introduce `Directory.Build.props` for shared `PropertyGroup` settings. Rejected as scope creep here - `Directory.Packages.props` (versions only) is the one new MSBuild-props file this change adds; consolidating shared properties is a separate, later cleanup if desired.

**GitHub Actions workflow that runs `dotnet build SearchSystem.sln` (restore + build) on push and pull_request.**
Alternative considered: also run `dotnet test`. Rejected - there is no test project (Non-Goal above), so a test step would either no-op or need to be removed again once tests exist; add it when the test project lands.

## Risks / Trade-offs

- [Enabling `Nullable` may surface pre-existing nullability warnings in `Shared`/`indexer`/`ConsoleSearch`] → Fix them as part of this change (proposal.md - Impact) rather than suppressing, so the build stays warning-clean; if any turn out to be non-trivial (e.g. reveal an actual latent null-reference bug), pause and flag it rather than silently suppressing.
- [Npgsql 9→10 or Sqlite 8→10 could carry breaking API changes] → Neither `indexer` nor `ConsoleSearch` currently wires up `DatabasePostgres` at runtime (see the `default-db-to-sqlite` change already archived), and `DatabaseSqlite`/`DatabasePostgres` use only basic ADO.NET (`DbConnection`/`DbCommand`) surface area, which is stable across these versions - but the build in Task-Verification must still catch anything that breaks.
- [Central package management can conflict with per-project `VersionOverride` needs] → Not needed here; all three projects want the same versions, so this is a non-issue for this repo's current shape.

## Migration Plan

1. Add `Directory.Packages.props` with the two centrally-managed versions; remove `Version=` attributes from the two `.csproj` `PackageReference` entries.
2. Bump the two centrally-managed versions to 10.0.11 / 10.0.3.
3. Add `.editorconfig` at repo root.
4. Add `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` to all three `.csproj` files; fix any resulting warnings.
5. Add `.github/workflows/build.yml`.
6. Build the solution locally to confirm zero warnings and zero errors before considering the change done.

No rollback complexity - every step is a config/tooling file; reverting is a plain `git revert`.
