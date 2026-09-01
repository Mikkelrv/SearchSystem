using System;
using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// Splits document and query text into words with one shared rule set, so the indexer
    /// and the search console tokenize text identically. A word is a maximal run of
    /// characters containing none of the separators.
    /// </summary>
    public static class Tokenizer
    {
        private static readonly char[] Separators =
            " \\\r\n\t\"$'!,?;.:-_**+=)([]{}<>/@&%€#".ToCharArray();

        /// <summary>Yields the NFKC-normalized (case-preserving) words in <paramref name="text"/>.</summary>
        public static IEnumerable<string> Tokenize(string text)
        {
            foreach (var token in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
                yield return TextNormalizer.Normalize(token);
        }
    }
}
