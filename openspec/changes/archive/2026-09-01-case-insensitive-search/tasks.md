## 1. Shared helpers

- [x] 1.1 Add `Shared/TextNormalizer.cs` — `public static class TextNormalizer` with
  `Normalize(string) => value.Normalize(NormalizationForm.FormKC)` and
  `Fold(string) => Normalize(value).ToLowerInvariant()`. Verify: `dotnet build Shared`
  succeeds; a unit check shows `Fold("CAFÉ")` equals `Fold` of the decomposed spelling.
- [x] 1.2 Add `Shared/Tokenizer.cs` — `public static class Tokenizer` holding the
  separator set currently in `Crawler` and `IEnumerable<string> Tokenize(string text)`
  that splits on the separators, drops empties, and yields `TextNormalizer.Normalize`d
  tokens (case preserved). Verify: `dotnet build Shared` succeeds; `Tokenize("A-b, C")`
  yields `A`, `b`, `C`.

## 2. Indexer: normalized-term table

- [x] 2.1 In `indexer/Crawler.cs`, replace the private `separators` split in
  `ExtractWordsInFile` with `Shared.Tokenizer.Tokenize`, and key the in-memory `words`
  dictionary (and everything stored) by `TextNormalizer.Fold(token)`. Verify:
  `dotnet build indexer` succeeds; indexing one file with `The` and `the` produces a
  single `word` row.
- [x] 2.2 In `indexer/DatabaseSqlite.cs` and `indexer/DatabasePostgres.cs`, change the
  `word` table definition to `name TEXT UNIQUE` (drop the `VARCHAR(50)` in the SQLite
  one). Verify: `dotnet build` succeeds for the solution; inspecting a freshly created
  database shows a unique index on `word(name)`.
- [x] 2.3 Re-run the indexer against the configured data folder. Verify: it exits without
  error; `SELECT COUNT(*) FROM word` is non-zero and lower than a raw-token index of the
  same corpus; no two rows share a `name`.
  → Indexed `seData/medium`: 3034 documents, 25258 word rows, 0 duplicate `name`s,
  `sqlite_autoindex_word_1` present, every stored term already lower-case.

## 3. ConsoleSearch: resolve query words via SQL

- [x] 3.1 In `ConsoleSearch/DatabaseSqlite.cs`, reimplement `GetWordIds` to resolve each
  query word with a parameterised `SELECT id FROM word WHERE name = @folded` where
  `@folded = TextNormalizer.Fold(word)`; found → add id, not found → add to
  `outIgnored`. Delete the `mWords` field and the private `GetAllWords`. Keep the method
  signature and return type. Verify: `dotnet build` succeeds.
- [x] 3.2 Apply the identical change to `ConsoleSearch/DatabasePostgres.cs` (delete its
  `mWords` / `GetAllWords` too). Verify: `dotnet build` succeeds for the solution;
  ConsoleSearch no longer reads the full `word` table at startup (confirm by inspection).
- [x] 3.3 Verify end-to-end that default search is now case-insensitive: against the
  rebuilt index, `copenhagen`, `Copenhagen`, and `COPENHAGEN` return the same document
  set, and none of them is reported as a missing word on those hits.
  → `copenhagen` is absent from `seData/medium`; used `hello` / `Hello` / `HELLO`
  instead — all three return the same 29 documents, none reporting the term missing.

## 4. ConsoleSearch: the `--case` flag

- [x] 4.1 In `ConsoleSearch/App.cs`, after splitting the input line, set
  `bool caseSensitive = tokens.Contains("--case")`, remove `--case` from the query words,
  and pass `caseSensitive` into `SearchLogic.Search`. Verify: entering `--case Hello`
  searches only `Hello`; `--case` never appears in the results or the `Ignored:` line.
- [x] 4.2 In `ConsoleSearch/SearchLogic.cs`, add `bool caseSensitive` to `Search`. When
  `false`, behaviour is unchanged. When `true`, take the folded ranked candidates and, in
  rank order, re-tokenize each document's source file via `Shared.Tokenizer`, keeping a
  document if at least one non-ignored query word appears with its exact
  `TextNormalizer.Normalize`d form (mirroring the default "≥1 query word" rule); the hit
  count is the number of exact-case query words present; stop at `maxAmount` (removed in
  §6); skip documents whose file cannot be read; compute the missing-words list from the
  same file scan. Verify: `dotnet build` succeeds.
- [x] 4.3 Verify the `--case` scenarios against the rebuilt index: with a document
  containing `Hello` and another containing only `hello`, `--case Hello` returns only the
  first; the next line `hello` returns both; `Hello --case World` is treated as a
  case-sensitive search for `Hello` and `World`.
  → `--case Hello` → 26 docs (exact `Hello`, grep-confirmed); `--case hello` → 2 docs
  (exact `hello`); next line `hello` → 29 (flag did not persist); `Hello --case hello`
  → 28 (union, case-sensitive, `--case` position-independent).

## 5. End-to-end verification

- [x] 5.1 Full manual pass covering every `search-matching` spec scenario: NFKC
  composed/decomposed and full-width/ASCII matches, case-insensitive default, single
  count per query word in ranking, unknown word ignored, all four `--case` behaviours,
  a `--case` hit whose source file was deleted being omitted, and full (uncapped) result
  sets under both default and `--case` matching.
  → NFKC via a throwaway 3-file corpus: precomposed `café` and decomposed `café` both
  hit the same doc; bare `cafe` is ignored (accents kept); full-width `５` and ASCII `5`
  both hit. `meeting hello` → 308 docs, exactly 1 counted as 2 hits (single count per
  word). `xyzzynotaword` → `Ignored:` / 0 docs. `--case Hello` with one source file
  renamed away → 25 instead of 26 (unreadable doc omitted).

## 6. Return all matching documents

- [x] 6.1 In `ConsoleSearch/SearchLogic.cs`, remove the `maxAmount` parameter from
  `Search` and `CaseSensitiveHits`. The default flow builds a `DocumentHit` for every id
  from `GetDocuments` (drop the `GetRange` / `Math.Min` slice); `CaseSensitiveHits` walks
  every ranked candidate (drop the `result.Count == maxAmount` break). Verify:
  `dotnet build` succeeds.
- [x] 6.2 In `ConsoleSearch/App.cs`, drop the `10` argument in the `SearchLogic.Search`
  call. Verify: `dotnet build` succeeds.
- [x] 6.3 Verify against the rebuilt index: for a common word, the number of results
  equals `SELECT COUNT(DISTINCT docId) FROM Occ WHERE wordId = (SELECT id FROM word WHERE
  name = '<folded>')`; a `--case` search on that word returns every exact-case match with
  an accurate total-hits figure.
  → `hello` → 29 results = `COUNT(DISTINCT docId)` for the term (29). `--case Hello` →
  26, `--case hello` → 2, matching grep over the 29 source files exactly; no cap hit.
