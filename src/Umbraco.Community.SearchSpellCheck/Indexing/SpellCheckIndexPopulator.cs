using Examine;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
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
            int pageIndex = 0;
            int pageSize = 500;

            // Items are read shallowest first, so a parent has always been seen before its children. Something is
            // indexed only when it is published and so is everything above it, like the CMS's published content
            // populator. Walking from the root also reads the recycle bin, whose items have a parent of -20 and so
            // never count as live.
            var live = new HashSet<int>();

            do
            {
                content = _contentService
                    .GetPagedDescendants(Cms.Core.Constants.System.Root, pageIndex, pageSize, out _, ordering: Ordering.By("Path"))
                    .ToArray();

                var liveContent = new List<IContent>();
                foreach (IContent item in content)
                {
                    if (item.Published
                        && !item.Trashed
                        && (item.ParentId == Cms.Core.Constants.System.Root || live.Contains(item.ParentId)))
                    {
                        live.Add(item.Id);
                        liveContent.Add(item);
                    }
                }

                if (liveContent.Count > 0)
                {
                    var valueSets = _spellCheckValueSetBuilder.GetValueSets(liveContent.ToArray()).ToList();

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
