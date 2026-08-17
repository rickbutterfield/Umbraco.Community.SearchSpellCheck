using Examine;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;

namespace Umbraco.Community.SearchSpellCheck.Indexing
{
    public class SpellCheckIndexPopulator : IndexPopulator<IUmbracoIndex>
    {
        private readonly SpellCheckValueSetBuilder _spellCheckValueSetBuilder;
        private readonly IContentService _contentService;

        public SpellCheckIndexPopulator(
            SpellCheckValueSetBuilder spellCheckValueSetBuilder,
            IContentService contentService)
        {
            _spellCheckValueSetBuilder = spellCheckValueSetBuilder;
            _contentService = contentService;
        }

        /// <summary>
        ///     Claims only the spell check index.
        /// </summary>
        /// <remarks>
        ///     <see cref="IndexPopulator{TIndex}" /> registers a populator against <em>every</em> index assignable to
        ///     <typeparamref name="TIndex" /> unless this is overridden. Without it, rebuilding Umbraco's own
        ///     ExternalIndex or InternalIndex from the backoffice would fill them with spell check documents, which
        ///     carry only <c>word</c>, <c>id</c>, <c>nodeName</c> and <c>urlName</c> and would replace the real ones.
        /// </remarks>
        public override bool IsRegistered(IUmbracoIndex index) => index is SpellCheckIndex;

        protected override void PopulateIndexes(IReadOnlyList<IIndex> indexes)
        {
            IContent[] content;
            long totalRecords = 0;
            int rootNode = -1;
            int pageIndex = 0;
            int pageSize = 500;

            do
            {
                content = _contentService.GetPagedDescendants(rootNode, pageIndex, pageSize, out totalRecords).ToArray();

                if (content.Length > 0)
                {
                    var valueSets = _spellCheckValueSetBuilder.GetValueSets(content).ToList();

                    foreach (var index in indexes)
                    {
                        index.IndexItems(valueSets);
                    }
                }

                pageIndex++;
            }
            while (content.Length == pageSize);
        }
    }
}
