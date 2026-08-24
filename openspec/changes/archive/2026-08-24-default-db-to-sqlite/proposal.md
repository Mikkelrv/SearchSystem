## Why

Running the `indexer` or `ConsoleSearch` apps blocks on a console prompt ("Use SQLite (1) or Postgres (2) database?") before any work can happen. For the common case (local/dev usage), the user always answers SQLite. The prompt adds friction, blocks non-interactive runs (e.g. scripts, CI), and duplicates the same prompt logic in both apps.

## What Changes

- Both `indexer` and `ConsoleSearch` default to the SQLite database implementation without prompting.
- Remove the interactive `GetDatabase()` console prompt from `indexer/App.cs` and `ConsoleSearch/App.cs`.
- Postgres support is not removed from the codebase (`DatabasePostgres` classes remain available for future use), but it is no longer reachable via the interactive prompt. Selecting a non-default database is out of scope for this change.

## Capabilities

### New Capabilities
- `database-selection`: Governs how the indexer and search console choose which `IDatabase` implementation to use at startup.

### Modified Capabilities
(none - no existing specs cover this behavior yet)

## Impact

- `indexer/App.cs`: remove `GetDatabase()` prompt, construct `DatabaseSqlite` directly.
- `ConsoleSearch/App.cs`: remove `GetDatabase()` prompt, construct `DatabaseSqlite` directly.
- No changes to `IDatabase`, `DatabaseSqlite`, or `DatabasePostgres` implementations themselves.
- No breaking changes to public APIs; this only changes the startup behavior of the two console apps.
