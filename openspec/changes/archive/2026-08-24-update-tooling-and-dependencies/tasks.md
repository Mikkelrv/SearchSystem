## 1. Central package management

- [x] 1.1 Create `Directory.Packages.props` at repo root with `ManagePackageVersionsCentrally=true` and `PackageVersion` entries for `Microsoft.Data.Sqlite` (10.0.11) and `Npgsql` (10.0.3); verify by confirming the file exists at repo root
- [x] 1.2 Remove the `Version=` attributes from the `Microsoft.Data.Sqlite` and `Npgsql` `PackageReference` entries in `indexer/indexer.csproj` and `ConsoleSearch/ConsoleSearch.csproj`; verify by running `dotnet restore SearchSystem.sln` and confirming it resolves versions from `Directory.Packages.props` with no errors

## 2. Coding-convention enforcement

- [x] 2.1 Add a repo-root `.editorconfig` encoding the naming/style rules already declared in `openspec/config.yaml` (PascalCase for types/methods/properties, camelCase for locals/params, `_camelCase` for private fields, `var` only when type is obvious); verify by confirming the file exists and an IDE/`dotnet format` picks it up

## 3. Nullable and implicit usings

- [x] 3.1 Add `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` to `Shared/Shared.csproj`, `indexer/indexer.csproj`, and `ConsoleSearch/ConsoleSearch.csproj`; verify by running `dotnet build SearchSystem.sln` and reviewing the warning list
- [x] 3.2 Fix any nullability/unused-`using` warnings surfaced by 3.1 in the affected source files; verify by running `dotnet build SearchSystem.sln` and confirming zero warnings

## 4. Continuous integration

- [x] 4.1 Add `.github/workflows/build.yml` that checks out the repo, sets up the .NET 10 SDK, and runs `dotnet restore` + `dotnet build SearchSystem.sln` on `push` and `pull_request`; verify by confirming the workflow YAML is valid (e.g. `actionlint` or a dry review) and that it targets `SearchSystem.sln`

## 5. Verification

- [x] 5.1 Run `dotnet build SearchSystem.sln` and confirm 0 errors, 0 warnings, and that the NU1903 `SQLitePCLRaw.lib.e_sqlite3` advisory no longer appears
