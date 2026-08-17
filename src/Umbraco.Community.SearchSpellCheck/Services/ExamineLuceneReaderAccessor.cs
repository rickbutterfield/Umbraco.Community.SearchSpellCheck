using Examine;
using Examine.Lucene.Providers;
using Examine.Lucene.Search;
using Lucene.Net.Index;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.SearchSpellCheck.Interfaces;

namespace Umbraco.Community.SearchSpellCheck.Services
{
    /// <summary>
    ///     Resolves the spell check index through Examine rather than by guessing where its files live.
    /// </summary>
    /// <remarks>
    ///     The previous implementation built a <c>SimpleFSDirectory</c> over
    ///     <c>umbraco/Data/TEMP/ExamineIndexes/{indexName}</c>. That is only correct for
    ///     <see cref="Umbraco.Cms.Core.Configuration.Models.LuceneDirectoryFactory.Default" />. Under
    ///     <c>TempFileSystemDirectoryFactory</c> the index actually lives in
    ///     <c>%TEMP%/ExamineIndexes/{appDomainHash}/{indexName}</c>, so suggestions silently returned nothing, and
    ///     under <c>SyncedTempFileSystemDirectoryFactory</c> the path holds a copy that only catches up on commit.
    ///     Asking Examine for the index works for all three.
    /// </remarks>
    internal class ExamineLuceneReaderAccessor : ILuceneReaderAccessor
    {
        private readonly IExamineManager _examineManager;
        private readonly IOptionsMonitor<SpellCheckOptions> _options;
        private readonly ILogger<ExamineLuceneReaderAccessor> _logger;

        public ExamineLuceneReaderAccessor(
            IExamineManager examineManager,
            IOptionsMonitor<SpellCheckOptions> options,
            ILogger<ExamineLuceneReaderAccessor> logger)
        {
            _examineManager = examineManager;
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public T Read<T>(Func<IndexReader, T> read, T valueIfUnavailable)
        {
            var indexName = _options.CurrentValue.IndexName;

            if (!_examineManager.TryGetIndex(indexName, out IIndex? index))
            {
                // Reachable during startup, before the index is registered, and on a misconfigured IndexName.
                // A missing index is not worth throwing over on a front end search request.
                _logger.LogWarning("Spell check index {IndexName} was not found, so no suggestions can be made.", indexName);
                return valueIfUnavailable;
            }

            if (index is not LuceneIndex luceneIndex)
            {
                _logger.LogWarning(
                    "Spell check index {IndexName} is a {IndexType}, which is not Lucene backed, so no suggestions can be made.",
                    indexName,
                    index.GetType().Name);
                return valueIfUnavailable;
            }

            if (luceneIndex.Searcher is not LuceneSearcher luceneSearcher)
            {
                _logger.LogWarning("Spell check index {IndexName} has no Lucene searcher, so no suggestions can be made.", indexName);
                return valueIfUnavailable;
            }

            ISearchContext searchContext = luceneSearcher.GetSearchContext();
            using ISearcherReference searcherReference = searchContext.GetSearcher();

            return read(searcherReference.IndexSearcher.IndexReader);
        }
    }
}
