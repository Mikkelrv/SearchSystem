## 1. NuGet package updates

- [x] 1.1 Bump `Microsoft.Data.Sqlite` from 8.0.1 to 10.0.11 in `indexer/indexer.csproj` and
      `ConsoleSearch/ConsoleSearch.csproj`, and verify `dotnet build SearchSystem.sln` succeeds
- [x] 1.2 Bump `Npgsql` from 9.0.3 to 10.0.3 in `indexer/indexer.csproj` and
      `ConsoleSearch/ConsoleSearch.csproj`, and verify `dotnet build SearchSystem.sln` succeeds
- [x] 1.3 Run `dotnet list SearchSystem.sln package --outdated` and
      `dotnet list SearchSystem.sln package --vulnerable --include-transitive` and verify the
      `SQLitePCLRaw.lib.e_sqlite3` (GHSA-2m69-gcr7-jv3q) advisory no longer appears

## 2. Central Package Management

- [x] 2.1 Add `Directory.Packages.props` at the solution root with
      `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and
      `<PackageVersion>` entries for `Microsoft.Data.Sqlite` and `Npgsql` at the versions from
      section 1
- [x] 2.2 Remove the `Version="..."` attributes from the `PackageReference` items in
      `indexer/indexer.csproj` and `ConsoleSearch/ConsoleSearch.csproj`, leaving only
      `Include="..."`, and verify `dotnet restore SearchSystem.sln` succeeds with versions
      resolved from `Directory.Packages.props`

## 3. SDK pinning

- [x] 3.1 Add `global.json` at the solution root pinning `sdk.version` to the installed SDK
      (`10.0.400`), and verify `dotnet --version` (run from the repo root) reports that version

## 4. Editor configuration

- [x] 4.1 Add a root `.editorconfig` encoding the naming conventions from
      `openspec/config.yaml` (PascalCase for types/methods/public members, camelCase for
      locals/parameters, `_camelCase` for private fields, `I`-prefixed interfaces) as C#
      naming-convention rules, and verify it loads without warnings by opening the solution in
      an editor that respects `.editorconfig` (or running `dotnet format --verify-no-changes`
      if available)

## 5. Continuous integration

- [x] 5.1 Add `.github/workflows/build.yml` that runs `dotnet restore` and
      `dotnet build --no-restore` for `SearchSystem.sln` on push and pull_request against
      `main`, and verify the workflow YAML is valid (e.g. `actionlint` or a GitHub Actions
      lint/dry-run if available)

## 6. Full verification

- [x] 6.1 Run `dotnet build SearchSystem.sln` from a clean checkout and verify all three
      projects (`Shared`, `indexer`, `ConsoleSearch`) build successfully. Note: scope was
      expanded with user approval mid-implementation (see tasks.md history / conversation) to
      include a whitespace-only `dotnet format` pass for task 4.1 - verified via
      `git diff -w` that all `.cs` changes are whitespace/encoding/brace-placement only, no
      identifiers or logic changed
