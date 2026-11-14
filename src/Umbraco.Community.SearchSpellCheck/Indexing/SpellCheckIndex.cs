using Examine.Lucene;
using Examine;
using Examine.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Cms.Core.Hosting;

namespace Umbraco.Community.SearchSpellCheck.Indexing
{
    /// <remarks>
    ///     This index deliberately does <em>not</em> implement <see cref="IUmbracoContentIndex" />, even though it
    ///     holds content. That marker interface is how Umbraco decides which indexes it owns:
    ///     <c>ExamineUmbracoIndexingHandler</c> writes to every <c>Indexes.OfType&lt;IUmbracoContentIndex&gt;()</c> on
    ///     publish, and <c>ContentIndexPopulator</c> / <c>MediaIndexPopulator</c> both derive from
    ///     <c>IndexPopulator&lt;IUmbracoContentIndex&gt;</c>. Carrying the marker meant Umbraco filled this index with
    ///     standard content documents that have no <c>word</c> field, competing with our own populator and handler for
    ///     the same document ids. Inheriting <see cref="UmbracoExamineIndex" /> still gives us
    ///     <c>IUmbracoIndex</c>, so the backoffice Examine dashboard continues to see the index.
    /// </remarks>
    internal class SpellCheckIndex : UmbracoExamineIndex, IIndex
    {
        public SpellCheckIndex(
            ILoggerFactory loggerFactory,
            string name,
            IOptionsMonitor<LuceneDirectoryIndexOptions> indexOptions,
            IHostingEnvironment hostingEnvironment,
            IRuntimeState runtimeState)
            : base(loggerFactory, name, indexOptions, hostingEnvironment, runtimeState)
        {
            LuceneDirectoryIndexOptions namedOptions = indexOptions.Get(name);
            if (namedOptions == null)
            {
                throw new InvalidOperationException($"No named {typeof(LuceneDirectoryIndexOptions)} options with name {name}");
            }

            if (namedOptions.Validator is IContentValueSetValidator contentValueSetValidator)
            {
                PublishedValuesOnly = contentValueSetValidator.PublishedValuesOnly;
            }
        }

        /// <summary>
        ///     Explicitly implemented so that callers holding an <see cref="IIndex" /> reach
        ///     <see cref="PerformIndexItems" /> directly. The base implementation would drop only failed value sets
        ///     and pass the filtered ones through, and <see cref="PerformIndexItems" /> validates for itself.
        /// </summary>
        void IIndex.IndexItems(IEnumerable<ValueSet> values) => PerformIndexItems(values, OnIndexOperationComplete);

        /// <summary>
        ///     Indexes valid content, and deletes content the validator filters out.
        /// </summary>
        /// <remarks>
        ///     Examine only skips <see cref="ValueSetValidationStatus.Failed" /> value sets. Content in the recycle bin
        ///     or under protected access comes back <see cref="ValueSetValidationStatus.Filtered" />, and
        ///     <c>UmbracoContentIndex</c> deletes those so a page that moves somewhere it must not be indexed loses
        ///     its words. This does the same. Anything that is not content is dropped.
        /// </remarks>
        protected override void PerformIndexItems(IEnumerable<ValueSet> values, Action<IndexOperationEventArgs> onComplete)
        {
            var valid = new List<ValueSet>();
            var filteredIds = new List<string>();

            foreach (ValueSet value in values.Where(x => x.Category == IndexTypes.Content))
            {
                ValueSetValidationResult result = ValueSetValidator?.Validate(value)
                    ?? new ValueSetValidationResult(ValueSetValidationStatus.Valid, value);

                switch (result.Status)
                {
                    case ValueSetValidationStatus.Valid:
                        // The validator's value set has unpublished cultures removed.
                        valid.Add(result.ValueSet);
                        break;
                    case ValueSetValidationStatus.Filtered:
                        filteredIds.Add(value.Id);
                        break;
                }
            }

            if (filteredIds.Count > 0)
            {
                base.PerformDeleteFromIndex(filteredIds, null);
            }

            if (valid.Count > 0)
            {
                base.PerformIndexItems(valid, onComplete);
            }
            else
            {
                onComplete(new IndexOperationEventArgs(this, 0));
            }
        }

        /// <summary>
        ///     Deletes items and everything beneath them.
        /// </summary>
        /// <remarks>
        ///     Unpublishing, trashing or deleting a page removes its descendants from the site too. Their
        ///     <c>__Path</c> values (written from <c>path</c> by <see cref="UmbracoExamineIndex" />) say which
        ///     documents sit beneath an id.
        /// </remarks>
        protected override void PerformDeleteFromIndex(IEnumerable<string> itemIds, Action<IndexOperationEventArgs>? onComplete)
        {
            var ids = itemIds.ToList();
            var all = new HashSet<string>(ids);

            foreach (var id in ids)
            {
                // Matches ",{id}," anywhere beneath the root, without matching ids that merely contain this one.
                var query = $@"{UmbracoExamineFieldNames.IndexPathFieldName}:(\-1\,{id}\,* OR \-1\,*\,{id}\,*)";

                // Examine returns 100 hits unless asked for more, so page through all of them. Deletes are queued
                // rather than applied here, so the hits do not shift between pages.
                const int pageSize = 500;
                var skip = 0;
                long total;
                do
                {
                    ISearchResults results = Searcher.CreateQuery().NativeQuery(query)
                        .SelectField("id")
                        .Execute(QueryOptions.SkipTake(skip, pageSize));
                    total = results.TotalItemCount;

                    foreach (ISearchResult result in results)
                    {
                        all.Add(result.Id);
                    }

                    skip += pageSize;
                }
                while (skip < total);
            }

            base.PerformDeleteFromIndex(all, onComplete);
        }
    }
}
