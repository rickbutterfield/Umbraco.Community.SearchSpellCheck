using Lucene.Net.Store;
using Umbraco.Community.SearchSpellCheck.Models;

namespace Umbraco.Community.SearchSpellCheck.Interfaces
{
    public interface ISuggestionService
    {
        /// <summary>
        ///     Returns a corrected version of <paramref name="searchTerm" />, or an empty string if nothing was
        ///     corrected.
        /// </summary>
        /// <param name="searchTerm">The term the visitor searched for. May be multiple words.</param>
        /// <param name="numberOfSuggestions">How many candidates to consider per word.</param>
        /// <param name="suggestionAccuracy">
        ///     Minimum score, from 0 to 1, a candidate must reach before it replaces the visitor's word.
        /// </param>
        /// <param name="culture">Culture to read variant fields for, or <c>null</c> for invariant content.</param>
        /// <remarks>
        ///     An empty result means "no correction to offer", which is what the documented
        ///     <c>if (!string.IsNullOrEmpty(Model.SpellCheck))</c> usage expects. Words with no suggestion above the
        ///     threshold are kept as the visitor typed them rather than dropped.
        /// </remarks>
        string GetSuggestion(string searchTerm, int numberOfSuggestions = 10, float suggestionAccuracy = 0.75f, string? culture = null);

        /// <summary>
        ///     Returns the scored candidates for a single <paramref name="word" />, best first.
        /// </summary>
        IReadOnlyList<Suggestion> GetSuggestions(string word, int numberOfSuggestions = 10, string? culture = null);

        [Obsolete("Use GetSuggestions, which returns a materialised list. This member returns a lazy sequence, which was previously enumerated after the underlying Lucene reader had been released. Scheduled for removal in 2.0.")]
        IOrderedEnumerable<Suggestion> SuggestionData(string word, int numberOfSuggestions = 10, string? culture = null);

        [Obsolete("Scoring is an implementation detail and the weighting is no longer a plain average. Read Suggestion.Priority instead. Scheduled for removal in 2.0.")]
        float? Priority(Suggestion metric);

        [Obsolete("The index is now resolved through IExamineManager. This method assumes the Default LuceneDirectoryFactory layout and returns the wrong path under TempFileSystemDirectoryFactory. Scheduled for removal in 2.0.")]
        SimpleFSDirectory GetFileSystemLuceneDirectory(string indexName);
    }
}
