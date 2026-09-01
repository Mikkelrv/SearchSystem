## Context

See `proposal.md` — Why. Current matching path:

- `indexer/Crawler.ExtractWordsInFile` splits file text on a private `separators` array
  and stores each raw word. `word.name` holds the exact bytes seen; `word` has one row
  per distinct raw form.
- `ConsoleSearch` loads the whole `word` table into a `Dictionary<string,int>`
  (`name -> id`) once at startup; `GetWordIds` resolves each query word with `ContainsKey`
  (ordinal, case-sensitive).
- `SearchLogic.Search` passes the resulting word ids to `IDatabase.GetDocuments` (ranks
  documents by `COUNT(wordId)` — i.e. number of query words present), `IDatabase.getMissing`
  (per-document set difference), and `IDatabase.WordsFromIds` (for the "missing" display).
- `DatabaseSqlite` / `DatabasePostgres` in ConsoleSearch have byte-identical
  `GetWordIds` / `GetAllWords` bodies.

Constraints: keep the slice small (project KISS / smallest-slice mandate); no new
dependencies; the change must sit on the path toward synonym search, relevance scoring,
and snippets, and must not reintroduce a whole-vocabulary in-memory load on the search
side.

## Goals / Non-Goals

**Goals:**

- Case-insensitive matching by default, NFKC-based.
- `--case` per-query opt-out for case-sensitive matching.
- One normalization implementation and one tokenizer, shared by indexer and search.
- Query-word resolution via an indexed database lookup, not an in-memory dictionary.
- A `word` table that is already a normalized-term table, so the scoring and synonym
  slices extend it rather than reshape it.
- Return every document that contains a query word, not a fixed-size top page.

**Non-Goals:**

- Relevance scoring, term frequencies, synonym expansion, snippets, positional index —
  each is its own later slice. This change does not add a `tf` column or positions.
- Renaming `word` / `word.name` to `term` / `normalized` — deferred to the scoring slice
  to keep this diff reviewable; the row's *meaning* changes here, the identifiers do not.
- Removing the indexer's in-memory term dictionary during a batch index run.
- Pagination or result streaming — every match is materialized in one result.
- Accent- or diacritic-insensitive search (NFKC keeps accents; only compatibility folding
  and case folding happen).
- Locale-aware casing (`ToLowerInvariant` only).
- A general CLI option parser — `--case` is a fixed sentinel token.

## Decisions

### D1: Shared `TextNormalizer` static helper

`Shared` gains `TextNormalizer` with two pure methods:

- `Normalize(string word)` → `word.Normalize(NormalizationForm.FormKC)` — NFKC, case preserved.
- `Fold(string word)` → `Normalize(word).ToLowerInvariant()` — NFKC + case fold.

Rationale: DRY — the indexer and search must agree on normalization or nothing matches.
Static pure functions match the project's "prefer pure functions" guidance; an injected
interface would be over-abstraction for a deterministic BCL call with no alternative
implementation. `Shared` is already the cross-project home (`BEDocument`, `Paths`).

### D2: Shared `Tokenizer`

Move the crawler's `separators` array and its split logic into `Shared.Tokenizer`:
`IEnumerable<string> Tokenize(string text)` yielding `TextNormalizer.Normalize`-d,
non-empty tokens (case preserved). The crawler consumes it; the `--case` post-filter (D5)
consumes it to re-tokenize source files with exactly the same rules.

Rationale: DRY and correctness — two independent tokenizers would drift and silently
change which documents match. This also lays the groundwork the snippet slice will need
for reading and scanning source files.

Alternative considered: leave tokenization in the crawler and have the `--case` filter
approximate it (e.g. `string.Split`). Rejected — different tokenization means
`--case Hello` could disagree with the index about whether `Hello` is even a word.

### D3: `word` is a normalized-term table

- Indexer stores `TextNormalizer.Fold(token)` in `word.name` — one row per case-folded
  NFKC term. The crawler already deduplicates words in memory before insert, so keying
  that dictionary by the folded form is the only change needed to collapse case variants.
- `word.name` gets a `UNIQUE` constraint (SQLite and Postgres both create an implicit
  index for it — exactly the index `GetWordIds` needs).
- `Occ(wordId, docId)` is unchanged: still a set, one row per (term, document).

Rationale: this is the smallest schema that (a) gives one id per query word — so
`GetDocuments`' `COUNT(wordId)` ranking stays correct with no code change — and (b) is
the shape synonyms (term-id references) and scoring (add `tf` to `Occ`) build on.

Alternatives considered:

- **Keep one row per surface form + an in-memory folded lookup** (the earlier plan).
  Smaller diff, but keeps the whole vocabulary in RAM on the search side and makes a
  query word resolve to several ids — inflating ranking and complicating the missing-words
  report. Rejected against the scale and roadmap goals.
- **One row per surface form + `surface` column on `Occ` for `--case`.** Clean
  case-sensitive queries, but `Occ` is the largest table and a per-row string roughly
  doubles it; it also collides with the scoring slice's plan to key `Occ` by `(term, doc)`
  with a `tf`. Rejected — see D5 for the chosen `--case` approach.

### D4: Query-word resolution via SQL

`IDatabase.GetWordIds(string[] query, out List<string> outIgnored)` keeps its signature
and its return type (`List<int>`). Each ConsoleSearch database implementation resolves a
query word with a parameterised lookup:

```sql
SELECT id FROM word WHERE name = @folded   -- @folded = TextNormalizer.Fold(queryWord)
```

Found → add the id; not found → add to `outIgnored`. The `mWords` field and the private
`GetAllWords` in both ConsoleSearch database classes are deleted.

Rationale: constant memory regardless of vocabulary size; N short indexed lookups per
search where N = number of query words. No `caseSensitive` parameter here — resolution is
always by folded form; case sensitivity is enforced later (D5).

### D5: `--case` as a post-filter over ranked results

`SearchLogic.Search` gains `bool caseSensitive`.

- **Default (`caseSensitive == false`):** unchanged flow — folded resolution (D4),
  `GetDocuments`, `getMissing`, `WordsFromIds`. One id per query word, so ranking and the
  missing-words report are already correct.
- **`--case` (`caseSensitive == true`):** run the same folded search to get the ranked
  candidate documents, then walk them in rank order and, for each, tokenize its source
  file (`document.mUrl`) via `Shared.Tokenizer` and keep the document if **at least one**
  non-ignored query word occurs in it with the exact `TextNormalizer.Normalize`d form
  (mirroring the default "contains ≥1 query word" rule). The kept document's hit count is
  the number of exact-case query words present; its missing words are the non-ignored
  query words absent from that same file scan, plus the ignored words. A document whose
  file cannot be read is skipped. The walk covers every ranked candidate.

Rationale: keeps all casing data out of the index (D3), so `Occ` stays lean for scoring.
Case-sensitive search is a secondary feature; paying for it at query time is the right
trade. The shared tokenizer (D2) keeps the file scan consistent with how the document was
indexed.

The walk covers every ranked candidate (D7), so the total-hits figure reported for a
`--case` search is an exact count of case-verified documents. Every candidate's source
file is read on each `--case` search; acceptable for this corpus, and the `occ_surface`
side table (below) is the fallback if it is measured too slow.

Alternatives considered: `surface` column on `Occ` (D3, rejected — table bloat); a
separate `occ_surface(wordId, docId, surface)` side table (deferred — add only if the
post-filter is measured too slow in practice).

### D6: `--case` parsing in `App.cs`

After `input.Split(" ", RemoveEmptyEntries)`:

```csharp
bool caseSensitive = tokens.Contains("--case");
string[] query = tokens.Where(t => t != "--case").ToArray();
```

`--case` is a fixed sentinel, not a flag grammar (YAGNI). A user cannot search for the
literal token `--case`; acceptable — the tokenizer splits on `-`, so `--case` is never an
indexed term anyway.

### D7: The result set is unbounded

`SearchLogic.Search` drops its `maxAmount` parameter and `App` passes no limit. The
default flow composes a `DocumentHit` for every id from `GetDocuments`; the `--case`
flow (D5) walks every ranked candidate.

Rationale: users want every document that contains a query word, not a page of them.
`GetDocuments` already returns all matching ids in rank order, so the default flow only
drops its `GetRange` slice — at the cost of one extra `GetDocDetails` + `getMissing`
round-trip per additional result.

Folded into this change rather than a separate slice because it directly removes the
`--case` walk's stop condition introduced in D5; the two are not independently shippable.

Trade-off: a broad query (a common word) returns and prints a long list, and `--case`
reads every candidate file. No pagination — YAGNI until the console UI needs it.

## Roadmap fit

- **Synonyms:** `word.id` is now a stable normalized-term id. A later `synonym` table
  references these ids; query resolution expands one id to a set before `GetDocuments`.
- **Scoring:** the scoring slice adds `tf` to `Occ` (and document length), and computes
  document frequency per term. `word` needs no further reshaping.
- **Snippets:** the snippet slice reads source files and locates terms — the
  `Shared.Tokenizer` and the file-reading path introduced here for `--case` are the same
  machinery. It will decide then whether a positional index is worth adding.

## Risks / Trade-offs

- **`--case` re-reads source files at query time** → every ranked candidate is read, one
  file per document that contains a query word; shared tokenizer keeps it correct;
  unreadable files degrade gracefully (document skipped, per spec).
- **Unbounded result set** → a common-word query materializes and prints a long list, and
  `--case` reads every candidate file. Accepted per the explicit product requirement;
  pagination deferred.
- **Indexer holds the term dictionary in RAM during a batch index** → not addressed here;
  it is a batch job that can be given resources, unlike the search REPL. Streaming
  upserts against the `UNIQUE` index is a separate future slice.
- **`word.name UNIQUE` vs. the crawler's in-memory dedupe** → consistent: the crawler
  already inserts each folded term once, so there is no constraint conflict; the
  constraint is a safety net and the lookup index.
- **`ToLowerInvariant` is not full Unicode case folding** → acceptable for this project;
  covers the ASCII/Latin case and matches the "keep it simple" constraint.
- **Stale indexes silently mismatch after upgrade** → stored forms and schema both
  change; the re-index is called out in `proposal.md` and `tasks.md`.

## Migration Plan

1. Ship `Shared` helpers + indexer changes + ConsoleSearch changes together (only
   meaningful as a set).
2. Re-run the indexer to rebuild the SQLite (and, if used, Postgres) index.
3. Rollback: revert the code and re-run the previous indexer to restore the old index.
   No in-place schema migration either way — the indexer drops and recreates all tables.
