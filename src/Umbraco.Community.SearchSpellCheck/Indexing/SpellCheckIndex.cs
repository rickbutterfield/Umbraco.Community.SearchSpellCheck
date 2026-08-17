using Examine.Lucene;
using Examine;
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
    internal class SpellCheckIndex : UmbracoExamineIndex
    {
        public SpellCheckIndex(
            ILoggerFactory loggerFactory,
            string name,
            IOptionsMonitor<LuceneDirectoryIndexOptions> indexOptions,
            IHostingEnvironment hostingEnvironment,
            IRuntimeState runtimeState)
            : base(loggerFactory, name, indexOptions, hostingEnvironment, runtimeState)
        {
            loggerFactory.CreateLogger<SpellCheckIndex>();

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
        ///     Drops anything that is not content before it reaches the index.
        /// </summary>
        /// <remarks>
        ///     This was previously an explicit <c>IIndex.IndexItems</c> implementation, which only took effect when the
        ///     index was reached through an <see cref="IIndex" /> reference. Overriding the protected member instead
        ///     applies the filter whatever the caller holds a reference to.
        /// </remarks>
        protected override void PerformIndexItems(IEnumerable<ValueSet> values, Action<IndexOperationEventArgs> onComplete)
            => base.PerformIndexItems(values.Where(x => x.Category == IndexTypes.Content), onComplete);
    }
}
