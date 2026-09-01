using System.Text;

namespace Shared
{
    /// <summary>
    /// Unicode text normalization shared by the indexer and the search console.
    /// Both sides must normalize identically or query words will not match indexed words.
    /// </summary>
    public static class TextNormalizer
    {
        /// <summary>Applies NFKC normalization, preserving case.</summary>
        public static string Normalize(string value) => value.Normalize(NormalizationForm.FormKC);

        /// <summary>Applies NFKC normalization and folds case, for case-insensitive matching.</summary>
        public static string Fold(string value) => Normalize(value).ToLowerInvariant();
    }
}
