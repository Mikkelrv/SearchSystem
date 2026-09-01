using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;
using Shared.Model;

namespace ConsoleSearch
{
    public class SearchLogic
    {
        IDatabase mDatabase;

        public SearchLogic(IDatabase database)
        {
            mDatabase = database;
        }

        /* Perform search of documents containing words from query. The result contains
         * every matching document, ordered by the number of query words present. When
         * caseSensitive is true, a document counts as a hit for a query word only when it
         * contains that word with the exact (NFKC) casing.
         */
        public SearchResult Search(String[] query, bool caseSensitive)
        {
            DateTime start = DateTime.Now;

            // Convert words to wordids
            var wordIds = mDatabase.GetWordIds(query, out var ignored);

            if (wordIds.Count == 0) // no words know in index
                return new SearchResult(query, 0, new List<DocumentHit>(), ignored, DateTime.Now - start);

            // perform the search - get all docIds, ordered by number of query words matched
            var docIds = mDatabase.GetDocuments(wordIds);

            if (caseSensitive)
            {
                var caseHits = CaseSensitiveHits(query, ignored, docIds);
                return new SearchResult(query, caseHits.Count, caseHits, ignored, DateTime.Now - start);
            }

            // compose the result - one DocumentHit per matching document. Everything the
            // loop needs is fetched in two batch queries, not one query per document.
            var details = mDatabase.GetDocDetails(docIds.Select(p => p.Key).ToList());

            // p.Value is how many query words a document contains; when it holds all of them
            // nothing is missing, so only the rest need a missing-words lookup.
            var shortIds = docIds.Where(p => p.Value < wordIds.Count).Select(p => p.Key).ToList();
            var missingByDoc = mDatabase.GetMissingWords(shortIds, wordIds);

            List<DocumentHit> docresult = new List<DocumentHit>();
            foreach (var p in docIds)
            {
                BEDocument doc = details[p.Key];

                List<string> missing = missingByDoc.TryGetValue(p.Key, out var m)
                    ? new List<string>(m)
                    : new List<string>();
                missing.AddRange(ignored);
                docresult.Add(new DocumentHit(doc, p.Value, missing));
            }

            return new SearchResult(query, docIds.Count, docresult, ignored, DateTime.Now - start);
        }

        /* Walk every ranked candidate, re-reading each source file with the shared
         * tokenizer, and keep those that contain at least one query word with its exact
         * NFKC casing. A document whose file cannot be read is skipped.
         */
        private List<DocumentHit> CaseSensitiveHits(
            string[] query, List<string> ignored, List<KeyValuePair<int, int>> docIds)
        {
            var wanted = query
                .Where(w => !ignored.Contains(w))
                .Select(TextNormalizer.Normalize)
                .Distinct()
                .ToList();

            var result = new List<DocumentHit>();
            var details = mDatabase.GetDocDetails(docIds.Select(d => d.Key).ToList());

            foreach (var candidate in docIds)
            {
                BEDocument doc = details[candidate.Key];

                ISet<string> tokens;
                try
                {
                    tokens = new HashSet<string>(Tokenizer.Tokenize(File.ReadAllText(doc.mUrl)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                var present = wanted.Where(tokens.Contains).ToList();
                if (present.Count == 0)
                    continue;

                var missing = wanted.Where(w => !tokens.Contains(w)).ToList();
                missing.AddRange(ignored);
                result.Add(new DocumentHit(doc, present.Count, missing));
            }

            return result;
        }
    }
}
