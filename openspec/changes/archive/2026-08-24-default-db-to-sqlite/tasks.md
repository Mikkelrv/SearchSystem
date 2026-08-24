## 1. Indexer

- [x] 1.1 In `indexer/App.cs`, remove the `GetDatabase()` console prompt and construct `new DatabaseSqlite()` directly in `Run()`; verify by running the indexer and confirming it starts indexing without any console prompt

## 2. ConsoleSearch

- [x] 2.1 In `ConsoleSearch/App.cs`, remove the `GetDatabase()` console prompt and construct `new DatabaseSqlite()` directly in `Run()`; verify by running ConsoleSearch and confirming it goes straight to "enter search terms" without any console prompt

## 3. Verification

- [x] 3.1 Build the solution (`dotnet build SearchSystem.sln`) and confirm no compiler warnings about now-unused Postgres usings/references in the two `App.cs` files
