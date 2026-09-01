## Context

See `proposal.md` — Why (includes the measured baseline). The relevant code paths:

- `SearchLogic.Search` (non-`--case` branch) loops over `docIds`
  (`List<KeyValuePair<int,int>>` of `docId → query-words-present count`, already ranked by
  `GetDocuments`) and, per document, calls `IDatabase.GetDocDetails(int)`,
  `IDatabase.getMissing(int, List<int>)`, and `IDatabase.WordsFromIds(List<int>)`.
- `CaseSensitiveHits` also calls `GetDocDetails(int)` once per ranked candidate.
- `IDatabase` (ConsoleSearch) is document-at-a-time: `GetDocDetails(int)`,
  `getMissing(int, …)`, `WordsFromIds(…)`. Both SQLite and Postgres implementations share
  an `AsString(List<int>)` helper that renders `(1,2,3)` for inline `IN` lists.
- `Occ` has one index: `word_index ON Occ(wordId)`. `GetDocuments` relies on it.
  `getMissing` filters `Occ` by `docId` with no index for it, so every call scans all
  occurrence rows of the query words.

Constraints: no new dependencies; keep each slice independently shippable and verifiable
against the baseline; do not change which documents match, their ranking, hit counts, or
the missing/ignored output.

## Goals / Non-Goals

**Goals:**

- Standard-search latency governed by result-set size, not by query-word frequency.
- Under ~1–2 s for any single query on the `medium` (~3k-doc) index.
- Query count per search bounded by a small constant, not `O(matched documents)`.

**Non-Goals:**

- `--case` latency (already ~0.3 s worst case; disk-bound at large scale — separate).
- Result pagination / lazy detail loading (the `O(results)` cost of building and printing
  one `DocumentHit` per match). Noted below as the next bottleneck; out of scope here.
- Changing the ranking model, adding `tf`, stopwords, or a positional index.
- Postgres performance tuning beyond mirroring the SQLite changes.

## Decisions

### D1 (Slice 1): Use the count `GetDocuments` already returns

`GetDocuments` returns, per document, `COUNT(wordId)` = the number of resolved query words
present. In `SearchLogic.Search`, when that count equals `wordIds.Count` the document
contains every query word, so its missing-words list is exactly `ignored` — skip the
`getMissing` + `WordsFromIds` calls for it. This is a guard, not a feature removal: the
`Missing:` list is still computed and shown for every document that actually lacks a query
word (only possible on multi-word searches). The matched-vs-total count itself — the
ranking signal now and a scoring input later — is `p.Value`, untouched.

- Effect: for every **single-word** search, `wordIds.Count == 1` and every match has count
  `≥ 1`, so those two per-document queries never run. Single-word common-word searches
  (`the`, `to`) are the worst offenders today.
- Cost: one integer comparison per result. No interface change, no re-index.
- Alternative considered: nothing simpler exists; this is a guard on data already in hand.

### D2 (Slice 2): Composite index `Occ(docId, wordId)`

Add `CREATE INDEX occ_doc ON Occ(docId, wordId)` in both indexer database classes. Keep
`word_index ON Occ(wordId)`.

- Serves `getMissing`'s `WHERE … AND docId = X` (and slice 3's `WHERE docId IN (…)`) as an
  index seek instead of a scan. Column order `(docId, wordId)` puts the equality/`IN`
  column first and makes the index covering for `getMissing` (it selects only `wordId`).
- Alternative `(wordId, docId)`: would require one seek per query word rather than one seek
  per document; worse for the "given a document, which query words does it have" shape,
  and redundant with the existing `word_index`.
- Cost: index build time during indexing + disk. Requires a re-index — acceptable, the
  indexer already drops and recreates every table, and `case-insensitive-search` already
  forced a re-index.

### D3 (Slice 3): Batch the per-document loop

Replace the document-at-a-time `IDatabase` methods used by `SearchLogic` with batch forms:

| removed | added |
|---|---|
| `BEDocument? GetDocDetails(int docId)` | `IReadOnlyDictionary<int, BEDocument> GetDocDetails(IReadOnlyList<int> docIds)` |
| `List<int> getMissing(int docId, List<int> wordIds)` + `List<string> WordsFromIds(List<int>)` | `IReadOnlyDictionary<int, List<string>> GetMissingWords(IReadOnlyList<int> docIds, IReadOnlyList<int> wordIds)` |

- `GetDocDetails(list)` — one `SELECT … FROM document WHERE id IN (…)`.
- `GetMissingWords(list, wordIds)` — one `SELECT docId, wordId FROM Occ WHERE docId IN (…)
  AND wordId IN (…)` to learn which query words each document has, plus one
  `SELECT id, name FROM word WHERE id IN (wordIds)` for the names; the per-document
  difference (`wordIds` minus present) is computed in memory. Called only with the subset
  of documents that D1 flagged as short at least one word.
- `SearchLogic.Search` then builds every `DocumentHit` from these two dictionaries with no
  database calls in the loop. `CaseSensitiveHits` switches to the batch `GetDocDetails`
  up front (it already walks all candidates); its missing-words list still comes from the
  file scan, unchanged.
- Query count per standard search: **3** (`GetWordIds` aside) regardless of match count —
  `GetDocuments`, `GetDocDetails`, and (only if some document is short a word)
  `GetMissingWords`'s two.

**SOLID:** this narrows `IDatabase` rather than widening it — three single-purpose methods
replace three others, no parallel old/new surface (ISP). The caller gets simpler. The
interface contract does change shape ("a batch" not "one row"), which is why the old
methods are deleted outright rather than kept.

**Alternative considered — fold document details into `GetDocuments`** via a
`JOIN document` so the ranked query returns full rows and slice 3 needs no separate
`GetDocDetails` at all. Fewer queries still, and it would speed `--case` too. Rejected for
this slice: it changes `GetDocuments`' return type and ripples into both call sites at
once, making the isolated before/after measurement harder. Left as a follow-up once the
batch shape is in and measured.

**Alternative considered — keep the loop, just open one connection / prepared statement.**
Rejected: it removes per-call object allocation but not the `O(matched documents)` query
count, which is the actual scaling problem.

### D4: Verification method

Every slice is measured with the console's built-in `Time:` line against a fixed query
set that spans the frequency range: `hello` (29), `meeting` (280), `meeting houston`
(440), `houston agreement` (268), `the` (2,354), `to` (3,034), `enron meeting agreement`
(2,976). Record the row in `tasks.md` next to the slice. A slice that does not move its
target numbers is a signal to stop and re-investigate before proceeding.

## Risks / Trade-offs

- **Large inline `IN (…)` lists** (e.g. `to` → 3,034 ids) → SQLite and Postgres both parse
  multi-thousand-element literal `IN` lists, but there is an upper bound. Mitigation: if a
  query set hits it, chunk the id list into batches of ~500 inside the batch methods and
  merge; keep it internal to the `IDatabase` implementation.
- **Slice 2 needs a re-index** → called out in `proposal.md` and `tasks.md`; the indexer
  recreates all tables, so there is no partial-migration state.
- **Behaviour drift while "optimizing"** → the fixed query set is checked for identical
  document ids, counts, and missing/ignored output before and after each slice, not just
  timing.
- **`O(results)` materialization remains** → after slice 3 a query matching ~90k documents
  on a 1M-doc corpus is still slow to build and print. This is the real ceiling; the fix
  (lazy/paged detail loading, keeping the true total from the cheap id query) is a
  separate change and is noted so it is not forgotten.

## Migration Plan

1. Slice 1 — code only (`SearchLogic.cs`). Ship, measure.
2. Slice 2 — indexer index definitions. Ship, **re-run the indexer**, measure.
3. Slice 3 — `IDatabase` + both implementations + `SearchLogic.cs`. Ship, measure.
4. Rollback: each slice reverts independently. Slice 2 rollback also means re-running the
   previous indexer (or just `DROP INDEX occ_doc`).
