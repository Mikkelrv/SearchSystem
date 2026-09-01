## Why

After `case-insensitive-search` removed the result-count cap, a standard (non-`--case`)
search on a common word takes **5–8 seconds** on the 3,034-document `medium` index — over
the "instant, at most a few seconds" expectation, and it degrades as the corpus grows.

The ranked-document-id query itself is fast (**~5 ms** even when it matches all 3,034
documents). The cost is entirely in `SearchLogic.Search`'s loop over the results, which
fires ~3 database queries **per matched document**:

- `GetDocDetails` — one `SELECT * FROM document WHERE id = X`
- `getMissing` — `SELECT wordId FROM Occ WHERE wordId IN (…) AND docId = X`
- `WordsFromIds` — `SELECT name FROM word WHERE id IN (…)`

`Occ` is indexed only on `wordId`, so each `getMissing` call scans **every** occurrence
row for the query words and then filters by `docId`. Total work grows with
`matched documents × word frequency` — roughly quadratic for common words.

Measured baseline (`medium`, current code):

| query | documents | time |
|---|---|---|
| `hello` | 29 | 30 ms |
| `meeting` | 280 | 70 ms |
| `meeting houston` | 440 | 126 ms |
| `the` | 2,354 | 5,024 ms |
| `to` | 3,034 | 7,178 ms |
| `enron meeting agreement` | 2,976 | 8,147 ms |

## What Changes

Three independent optimizations, each shippable and measurable on its own. No change to
which documents match, their ranking, or the missing-words output — latency only.

- **Slice 1 — skip the missing-words lookup when its result is provably empty.**
  `GetDocuments` already returns, per document, the count of query words it contains. When
  that count equals the number of resolved query words the missing list is empty by
  definition, so `getMissing` / `WordsFromIds` are not called for that document. The
  `Missing:` output is unchanged everywhere it is non-empty (i.e. multi-word searches where
  a document lacks a word); single-word searches — where every match contains the one word
  and the lookup was always pure waste — stop doing it entirely.
- **Slice 2 — composite index on `Occ`.** Add `CREATE INDEX occ_doc ON Occ(docId, wordId)`
  so any `docId`-filtered occurrence lookup stops scanning. **BREAKING (index only)** — a
  re-index is required (the indexer drops and recreates all tables anyway).
- **Slice 3 — batch the per-document loop into set queries.** Replace the N-iteration loop
  with a fixed number of queries per search: one `document WHERE id IN (…all result ids…)`,
  one `Occ WHERE docId IN (…) AND wordId IN (…query ids…)` for the documents still short a
  word, and the query-word id→name map resolved once. Query count stops growing with the
  result-set size.

**Ranking (implementation order):** Slice 1 → Slice 2 → Slice 3, by impact per effort.
Verify search timings against the baseline table after each slice before starting the next.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none — `skip_specs: true`. This is a pure performance refactor: the documents returned,
their ranking, the hit counts, and the missing-words / ignored-words output are all
unchanged. No spec-level behavior changes.)

## Impact

- **Code**
  - `ConsoleSearch/SearchLogic.cs` — all three slices.
  - `ConsoleSearch/IDatabase.cs` + `DatabaseSqlite.cs` + `DatabasePostgres.cs` — slice 3
    replaces `GetDocDetails` / `getMissing` / `WordsFromIds` per-id calls with batch
    equivalents; the superseded single-id methods are removed.
  - `indexer/DatabaseSqlite.cs` + `indexer/DatabasePostgres.cs` — slice 2 adds the index.
- **Data** — slice 2 requires re-running the indexer.
- **No new dependencies.**
- **Design tradeoffs (SOLID / KISS)**
  - Slice 3 reshapes `IDatabase` from "one document at a time" to "a batch of documents at
    once". This is a net simplification of the caller, but it does change the interface
    contract; the old per-id methods are deleted rather than kept alongside (ISP — the
    interface stays focused, no dead surface).
  - `getMissing`'s per-document form becomes unused after slice 3 and is removed.
  - **Out of scope:** the search still builds one `DocumentHit` per matched document and
    the console still prints one block per hit, so a query matching 90,000 documents on a
    large corpus is still slow to *materialize and display* even with zero redundant
    queries. Fixing that means loading result details lazily as the user pages (keeping the
    true total from the cheap id query) — a separate change, noted in `design.md`.
