using Examine;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Community.SearchSpellCheck.Indexing;

namespace Umbraco.Community.SearchSpellCheck.Tests.Indexing
{
    internal static class TestValueSetBuilder
    {
        /// <summary>
        ///     A builder that returns an empty value set per content item, for tests that care which items reach the
        ///     index rather than what is in them.
        /// </summary>
        public static SpellCheckValueSetBuilder Create()
        {
            var builder = new Mock<SpellCheckValueSetBuilder>(
                Mock.Of<IOptionsMonitor<SpellCheckOptions>>(),
                NullLogger<SpellCheckValueSetBuilder>.Instance,
                new UrlSegmentProviderCollection(() => Array.Empty<IUrlSegmentProvider>()),
                Mock.Of<IShortStringHelper>(),
                new PropertyEditorCollection(new DataEditorCollection(() => Array.Empty<IDataEditor>())),
                Mock.Of<IContentTypeService>(),
                Mock.Of<ILanguageService>());

            builder
                .Setup(x => x.GetValueSets(It.IsAny<IContent[]>()))
                .Returns((IContent[] items) => items.Select(x =>
                    new ValueSet(x.Id.ToString(), IndexTypes.Content, new Dictionary<string, object>())));

            return builder.Object;
        }
    }
}
