## Purpose

Defines how the search console turns the words a user types into matches against
indexed documents: Unicode normalization of every word, case-insensitive matching by
default, and a per-query opt-out for case-sensitive matching.

## ADDED Requirements

### Requirement: Words are Unicode-normalized before matching

The system SHALL apply Unicode NFKC normalization to every word both when it is indexed
and when it appears in a search query, so that words that differ only in Unicode
normalization form are treated as the same word.

#### Scenario: Composed and decomposed forms match

- **WHEN** a document contains a word written with a precomposed character (for example `é`, U+00E9)
- **AND** the user searches for the same word written in decomposed form (`e` + U+0301)
- **THEN** the document is returned as a hit for that word

#### Scenario: Compatibility forms match

- **WHEN** a document contains a word using a compatibility character (for example the full-width digit `５`)
- **AND** the user searches for the ASCII equivalent (`5`)
- **THEN** the document is returned as a hit for that word

### Requirement: Query words are matched case-insensitively by default

In the absence of the `--case` token, the system SHALL match a query word against a
document whenever the document contains that word in any casing. A word that never
appears in any document, in any casing, SHALL be reported as ignored.

#### Scenario: Query case differs from document case

- **WHEN** a document contains the word `Copenhagen`
- **AND** the user searches for `copenhagen` (or `COPENHAGEN`)
- **THEN** the document is returned as a hit, and `copenhagen` is not listed among its missing words

#### Scenario: Each query word counts once toward ranking

- **WHEN** a document contains both `The` and `the`
- **AND** the user's case-insensitive query includes `the` once
- **THEN** that document's match count reflects `the` as a single matched query word

#### Scenario: Word absent in every casing is unknown

- **WHEN** the user searches for a word that does not appear in any document in any casing
- **THEN** that word is reported as ignored

### Requirement: The `--case` token makes a single search case-sensitive

The system SHALL treat the literal token `--case`, appearing anywhere among the
space-separated words of a search input line, as a request to match that line's query
words case-sensitively. The token SHALL be removed from the query words and SHALL NOT
itself be searched for. Under `--case`, a document is a hit for a query word only if it
contains that word with the same casing (after NFKC normalization). The setting SHALL
apply only to the search on that line.

#### Scenario: Case-sensitive query excludes differing case

- **WHEN** the user enters `--case Hello`
- **AND** a document contains `hello` but not `Hello`
- **THEN** that document is not returned as a hit for `Hello`

#### Scenario: Case-sensitive query includes exact case

- **WHEN** the user enters `--case Hello`
- **AND** a document contains `Hello`
- **THEN** that document is returned as a hit for `Hello`

#### Scenario: The flag does not persist to later searches

- **WHEN** the user enters `--case Hello` and then, on the next line, enters `hello`
- **THEN** the second search is case-insensitive and matches documents containing `hello` or `Hello`

#### Scenario: The flag position does not matter

- **WHEN** the user enters `Hello --case World`
- **THEN** the search is case-sensitive and the query words are `Hello` and `World`

#### Scenario: Source file needed for case check is unavailable

- **WHEN** a `--case` search matches a document whose source file can no longer be read
- **THEN** that document is omitted from the case-sensitive results rather than shown unverified

### Requirement: Every matching document is returned

The system SHALL return every document that contains at least one non-ignored query word,
under both default and `--case` matching, rather than a fixed-size page of the
highest-ranked documents. Documents SHALL be ordered by the number of distinct query words
they contain, descending.

#### Scenario: More matches than the former page size

- **WHEN** more than ten documents contain a query word
- **THEN** all of them are returned, ordered by number of matching query words descending

#### Scenario: Case-sensitive result set is not capped

- **WHEN** a `--case` search matches more than ten documents that contain a query word with the exact (NFKC) casing
- **THEN** all of them are returned, and the reported total is the exact number of case-verified documents
