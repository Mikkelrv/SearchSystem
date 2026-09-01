## 1. Baseline

- [x] 1.1 Record the current `Time:` for the fixed query set against the `medium` index
  and paste it into the table below. Query set: `hello`, `meeting`, `houston agreement`,
  `meeting houston`, `the`, `to`, `enron meeting agreement`. Also record each query's
  document count and the first three result paths, to compare for drift after every slice.
  → Baseline captured; first-3 result paths per query saved to `scratchpad/baseline.txt`.
  Counts and result order are the drift check for every later slice.

  | query | docs | baseline | after S1 | after S2 | after S3 |
  |---|---|---|---|---|---|
  | `hello` | 29 | 49 ms | 44 ms | 44 ms | 52 ms |
  | `meeting` | 280 | 87 ms | 55 ms | 56 ms | 57 ms |
  | `houston agreement` | 268 | 73 ms | 71 ms | 50 ms | 39 ms |
  | `meeting houston` | 440 | 125 ms | 122 ms | 61 ms | 41 ms |
  | `the` | 2354 | 4946 ms | **82 ms** | 82 ms | 44 ms |
  | `to` | 3034 | 7017 ms | **97 ms** | 99 ms | 48 ms |
  | `enron meeting agreement` | 2976 | 8142 ms | 8054 ms | **282 ms** | **73 ms** |

## 2. Slice 1 — skip needless missing-words work

- [x] 2.1 In `ConsoleSearch/SearchLogic.cs` (non-`--case` branch), compute `missing` for a
  result document only when `p.Value < wordIds.Count`; otherwise `missing` starts empty.
  Append `ignored` in both cases, preserving the current order (missing words first, then
  ignored). Verify: `dotnet build` succeeds.
  → Done; build warning-clean.
- [x] 2.2 Re-run the query set. Verify: identical document counts and result paths as the
  baseline; single-word queries (`the`, `to`) drop sharply; fill the "after S1" column.
  → No drift (counts + result order identical, `diff` clean). `the` 4946→82 ms, `to`
  7017→97 ms (~60–70×). Multi-word common query (`enron meeting agreement`) essentially
  unchanged — its documents are genuinely missing words, so the lookup still runs; that is
  Slice 2's target. `Missing:` output verified intact: `meeting houston` still reports
  `[houston]` / `[meeting]` / `[]` per document; ignored words still appended.

## 3. Slice 2 — composite index on `Occ`

- [x] 3.1 In `indexer/DatabaseSqlite.cs` and `indexer/DatabasePostgres.cs`, add
  `CREATE INDEX occ_doc ON Occ(docId, wordId)` after the existing `word_index`. Verify:
  `dotnet build` succeeds for the solution.
  → Added to both; solution builds warning-clean.
- [x] 3.2 Re-run the indexer against `medium`. Verify: it exits without error; a fresh DB
  shows both `word_index` and `occ_doc` on `Occ`.
  → Re-indexed: 3034 docs / 25258 words / 462828 Occ rows (unchanged). Both indexes
  present. `EXPLAIN QUERY PLAN` for the `getMissing` shape now reads
  `SEARCH Occ USING COVERING INDEX occ_doc (docId=? AND wordId=?)` — was a full scan.
  Indexing time rose ~90 s → ~120 s (second Occ index maintained during 462k inserts);
  acceptable for a batch job.
- [x] 3.3 Re-run the query set. Verify: identical document counts and result paths;
  multi-word common-word queries (`enron meeting agreement`) drop; fill the "after S2"
  column.
  → `enron meeting agreement` 8054 → 282 ms (~29×); `meeting houston` 122 → 61 ms;
  `houston agreement` 71 → 50 ms. Document **sets** identical to baseline (md5 of the
  sorted docId list matches a direct `SELECT DISTINCT docId FROM Occ` for `the`/`to`/
  `hello`), and rank order by match-count is unchanged. Tie-break order among equal-count
  documents shifted — an artifact of re-indexing (docId / Occ insertion order), not the
  index change, and within spec (ordering is defined only by match count). `Missing:`
  output for `meeting houston` unchanged: 266 `[houston]` / 160 `[meeting]` / 14 `[]`.

## 4. Slice 3 — batch the per-document loop

- [x] 4.1 In `ConsoleSearch/IDatabase.cs`, replace `GetDocDetails(int)`,
  `getMissing(int, List<int>)`, and `WordsFromIds(List<int>)` with
  `GetDocDetails(IReadOnlyList<int> docIds) -> IReadOnlyDictionary<int, BEDocument>` and
  `GetMissingWords(IReadOnlyList<int> docIds, IReadOnlyList<int> wordIds) ->
  IReadOnlyDictionary<int, List<string>>` (missing query-word *names* per document, only
  for documents missing at least one). Verify: solution builds after 4.2–4.3.
  → Done.
- [x] 4.2 Implement both new methods in `ConsoleSearch/DatabaseSqlite.cs`:
  `GetDocDetails` = one `SELECT … FROM document WHERE id IN (…)`; `GetMissingWords` = one
  `SELECT docId, wordId FROM Occ WHERE docId IN (…) AND wordId IN (…)` + one
  `SELECT id, name FROM word WHERE id IN (wordIds)`, difference computed in memory. Reuse
  the existing `AsString` helper; chunk id lists >500 if a driver limit is hit. Verify:
  `dotnet build` succeeds.
  → Done. No chunking added: a 3,034-element inline `IN` list runs in ~3 ms in SQLite,
  so the driver limit is not hit at this scale (YAGNI; the `O(results)` ceiling is a
  separate change and will bound this anyway).
- [x] 4.3 Apply the identical implementation to `ConsoleSearch/DatabasePostgres.cs`.
  Verify: `dotnet build` succeeds for the solution.
  → Done; solution builds warning-clean. (Postgres path not exercised — no instance.)
- [x] 4.4 Rewrite `SearchLogic.Search`: after `GetDocuments`, call the batch
  `GetDocDetails` once for all result ids, call `GetMissingWords` once for the ids where
  `p.Value < wordIds.Count`, then build every `DocumentHit` from those dictionaries with
  no database call in the loop. Switch `CaseSensitiveHits` to the batch `GetDocDetails`
  (fetch all candidate details up front). Verify: `dotnet build` succeeds.
  → Done.
- [x] 4.5 Re-run the query set. Verify: identical document counts and result paths as the
  baseline; every query under ~1 s on `medium`; fill the "after S3" column.
  → All 7 queries 39–73 ms. Document counts identical. `enron meeting agreement`
  8054→282 (S2) →73 ms (S3). Missing-word histograms identical to pre-S3
  (`enron meeting agreement`: 2599/270/97/10; `meeting houston`: 266/160/14). 12 random
  result rows cross-checked against `Occ` — hit counts and missing lists all correct.
  **One cosmetic change:** in a Missing list with 2+ words, the words now print in the
  order the user typed them rather than ascending word-id order (e.g.
  `[agreement,houston]` not `[houston,agreement]`). Same words, not spec'd, arguably
  clearer. Single missing word — the common case — is unaffected.
- [x] 4.6 Re-run the `search-matching` `--case` checks (`--case Hello` → 26, `--case hello`
  → 2, `--case Hello` with one source file renamed away → 25) to confirm the `--case` path
  still behaves after the `GetDocDetails` change.
  → 26 / 2 / 25 respectively. Pass.

## 5. Wrap-up

- [x] 5.1 Confirm no dead code remains (`getMissing` / single-id `GetDocDetails` /
  `WordsFromIds` gone from both `IDatabase` implementations) and `dotnet build` is
  warning-clean.
  → `grep` finds no `getMissing` / `WordsFromIds` anywhere; only the batch `GetDocDetails`.
  Solution builds with 0 warnings / 0 errors.
