using System.Collections.Generic;
using Shared.Model;

namespace ConsoleSearch
{
    public interface IDatabase
    {
        /// <summary>
        /// Get id's for words in [query]. [outIgnored] contains those word from query that is
        /// not present in any document.
        /// </summary>
        List<int> GetWordIds(string[] query, out List<string> outIgnored);

        /// <summary>
        /// Perform the essential search for documents. It will return
        /// a list of KeyValuePairs - the key is the id of the
        /// document, and value is the number of words from the query
        /// contained in the document. The list is ordrered for descending value.
        /// </summary>
        List<KeyValuePair<int, int>> GetDocuments(List<int> wordIds);

        /// <summary>
        /// Look up the details of many documents in one query, keyed by document id.
        /// Every id in [docIds] comes from <see cref="GetDocuments"/>, so every id is present
        /// in the result.
        /// </summary>
        IReadOnlyDictionary<int, BEDocument> GetDocDetails(IReadOnlyList<int> docIds);

        /// <summary>
        /// For each document in [docIds], the names of the query words (given as [wordIds])
        /// that the document does NOT contain, in [wordIds] order. Intended to be called only
        /// with documents already known to be missing at least one query word.
        /// </summary>
        IReadOnlyDictionary<int, List<string>> GetMissingWords(
            IReadOnlyList<int> docIds, IReadOnlyList<int> wordIds);
    }
}
