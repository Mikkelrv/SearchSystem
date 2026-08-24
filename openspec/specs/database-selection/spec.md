## Purpose

Defines how the indexer and search console choose which database implementation to use at startup, so both tools run without manual intervention by default.

## Requirements

### Requirement: Default database is SQLite
The indexer and search console SHALL use the SQLite database implementation by default when started, without requiring any user input to select a database.

#### Scenario: Indexer starts without prompting
- **WHEN** the indexer application is run
- **THEN** it uses the SQLite database implementation immediately, with no console prompt asking the user to choose a database

#### Scenario: Search console starts without prompting
- **WHEN** the ConsoleSearch application is run
- **THEN** it uses the SQLite database implementation immediately, with no console prompt asking the user to choose a database
