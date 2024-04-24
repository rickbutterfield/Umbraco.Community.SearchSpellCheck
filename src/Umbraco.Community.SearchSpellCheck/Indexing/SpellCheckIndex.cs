using Examine.Lucene;
using Examine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Cms.Core.Hosting;

namespace Umbraco.Community.SearchSpellCheck.Indexing
{
    internal class SpellCheckIndex : UmbracoExamineIndex, IUmbracoContentIndex, IDisposable
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

        void IIndex.IndexItems(IEnumerable<ValueSet> values)
        {
            var vals = values.Where(x => x.Category == IndexTypes.Content);
            PerformIndexItems(vals, OnIndexOperationComplete);
        }
    }
}
