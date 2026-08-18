using Examine;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Community.SearchSpellCheck.Indexing;
using static Umbraco.Cms.Core.Constants.PropertyEditors;

namespace Umbraco.Community.SearchSpellCheck.Tests.Indexing
{
    [TestFixture]
    public class SpellCheckValueSetBuilderTests
    {
        private const string PropertyAlias = "bodyText";

        /// <summary>
        ///     The field the spell checker builds its dictionary from.
        /// </summary>
        private static string WordsIn(ValueSet valueSet)
            => valueSet.Values.TryGetValue("word", out var values)
                ? string.Join(" ", values.Select(x => x?.ToString()))
                : string.Empty;

        [Test]
        public void Keeps_Every_Value_A_Property_Produces_Under_The_Same_Key()
        {
            // The regression this fixture exists for. A Block List or Block Grid produces many values under one
            // index key. The previous implementation stored them in a Dictionary<string, string> and replaced the
            // entry each time the key repeated, so only the last value survived and the rest never reached the
            // spelling dictionary. Umbraco's own BaseValueSetBuilder appends here.
            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.BlockList,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "alpha", "bravo", "charlie" }
                });

            Assert.That(WordsIn(valueSet), Is.EqualTo("alpha bravo charlie"));
        }

        [Test]
        public void Keeps_Values_From_Several_Keys()
        {
            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.BlockGrid,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "alpha" },
                    [PropertyAlias + "_sub"] = new object?[] { "bravo" }
                });

            Assert.That(WordsIn(valueSet).Split(' '), Is.EquivalentTo(new[] { "alpha", "bravo" }));
        }

        [Test]
        public void Removes_Duplicates_Regardless_Of_Casing()
        {
            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.TextBox,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "House", "house", "HOUSE", "garden" }
                });

            Assert.That(WordsIn(valueSet), Is.EqualTo("House garden"));
        }

        [Test]
        public void Skips_Udis_Whatever_Type_They_Arrive_As()
        {
            // The udi guard previously only ran for values that arrived as strings, so one surfacing as any other
            // type went into the dictionary as an identifier.
            var udi = new Mock<object>();
            udi.Setup(x => x.ToString()).Returns("umb://document/2b7f9b1f9a1c4c8f9a1c4c8f9a1c4c8f");

            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.TinyMce,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "alpha", "umb://media/abc", udi.Object }
                });

            Assert.That(WordsIn(valueSet), Is.EqualTo("alpha"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Skips_Empty_Values(string? value)
        {
            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.TextArea,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "alpha", value }
                });

            Assert.That(WordsIn(valueSet), Is.EqualTo("alpha"));
        }

        [Test]
        public void Skips_Values_Under_An_Empty_Key()
        {
            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.TextBox,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "alpha" },
                    ["  "] = new object?[] { "bravo" }
                });

            Assert.That(WordsIn(valueSet), Is.EqualTo("alpha"));
        }

        [Test]
        public void Ignores_Property_Editors_It_Cannot_Read_Words_From()
        {
            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.UploadField,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "alpha" }
                });

            Assert.That(WordsIn(valueSet), Is.Empty);
        }

        [Test]
        public void Matches_Configured_Aliases_Without_Regard_To_Casing()
        {
            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.TextBox,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "alpha" }
                },
                configuredFields: new List<string> { "BODYTEXT" });

            Assert.That(WordsIn(valueSet), Is.EqualTo("alpha"));
        }

        [Test]
        public void Ignores_Properties_That_Were_Not_Configured()
        {
            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.TextBox,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "alpha" }
                },
                configuredFields: new List<string> { "somethingElse" });

            Assert.That(WordsIn(valueSet), Is.Empty);
        }

        [Test]
        public void Writes_The_Standard_Identifying_Fields()
        {
            ValueSet valueSet = BuildSingleValueSet(
                editorAlias: Aliases.TextBox,
                indexValues: new Dictionary<string, IEnumerable<object?>>
                {
                    [PropertyAlias] = new object?[] { "alpha" }
                });

            Assert.Multiple(() =>
            {
                Assert.That(valueSet.Id, Is.EqualTo("1234"));
                Assert.That(valueSet.Category, Is.EqualTo(Cms.Infrastructure.Examine.IndexTypes.Content));
                Assert.That(valueSet.Values["nodeName"].Single(), Is.EqualTo("Test page"));
            });
        }

        #region Scaffolding

        private static ValueSet BuildSingleValueSet(
            string editorAlias,
            Dictionary<string, IEnumerable<object?>> indexValues,
            List<string>? configuredFields = null)
        {
            SpellCheckValueSetBuilder builder = CreateBuilder(editorAlias, indexValues, configuredFields);
            return builder.GetValueSets(CreateContent(editorAlias)).Single();
        }

        private static SpellCheckValueSetBuilder CreateBuilder(
            string editorAlias,
            Dictionary<string, IEnumerable<object?>> indexValues,
            List<string>? configuredFields)
        {
            var indexValueFactory = new Mock<IPropertyIndexValueFactory>();
            indexValueFactory
                .Setup(x => x.GetIndexValues(
                    It.IsAny<IProperty>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IDictionary<Guid, IContentType>>()))
                .Returns(indexValues);

            var editor = new Mock<IDataEditor>();
            editor.SetupGet(x => x.Alias).Returns(editorAlias);
            editor.SetupGet(x => x.PropertyIndexValueFactory).Returns(indexValueFactory.Object);

            // PropertyEditorCollection filters on (x.Type & EditorType.PropertyValue) > 0, so an editor that does
            // not declare this is silently dropped from the collection.
            editor.SetupGet(x => x.Type).Returns(EditorType.PropertyValue);

            var propertyEditors = new PropertyEditorCollection(new DataEditorCollection(() => new[] { editor.Object }));

            var urlSegmentProvider = new Mock<IUrlSegmentProvider>();
            urlSegmentProvider.Setup(x => x.GetUrlSegment(It.IsAny<IContentBase>(), It.IsAny<string?>())).Returns("test-page");

            var options = new SpellCheckOptions
            {
                IndexedFields = configuredFields ?? new List<string> { PropertyAlias },
                EnableLogging = false
            };

            var contentTypeService = new Mock<IContentTypeService>();
            contentTypeService.Setup(x => x.GetAll()).Returns(Array.Empty<IContentType>());

            var localizationService = new Mock<ILocalizationService>();
            localizationService.Setup(x => x.GetDefaultLanguageIsoCode()).Returns("en-US");

            return new SpellCheckValueSetBuilder(
                Mock.Of<IOptionsMonitor<SpellCheckOptions>>(x => x.CurrentValue == options),
                NullLogger<SpellCheckValueSetBuilder>.Instance,
                new UrlSegmentProviderCollection(() => new[] { urlSegmentProvider.Object }),
                Mock.Of<IShortStringHelper>(),
                propertyEditors,
                contentTypeService.Object,
                localizationService.Object);
        }

        private static IContent CreateContent(string editorAlias)
        {
            var propertyType = new Mock<IPropertyType>();
            propertyType.SetupGet(x => x.PropertyEditorAlias).Returns(editorAlias);
            propertyType.SetupGet(x => x.Alias).Returns(PropertyAlias);

            var property = new Mock<IProperty>();
            property.SetupGet(x => x.Alias).Returns(PropertyAlias);
            property.SetupGet(x => x.PropertyType).Returns(propertyType.Object);

            var contentType = new Mock<ISimpleContentType>();
            contentType.SetupGet(x => x.Alias).Returns("testPage");
            contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);

            var content = new Mock<IContent>();
            content.SetupGet(x => x.Id).Returns(1234);
            content.SetupGet(x => x.Key).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            content.SetupGet(x => x.Name).Returns("Test page");
            content.SetupGet(x => x.PublishName).Returns("Test page");
            content.SetupGet(x => x.ContentType).Returns(contentType.Object);
            content.SetupGet(x => x.AvailableCultures).Returns(Array.Empty<string>());
            content.SetupGet(x => x.Properties).Returns(new PropertyCollection(new[] { property.Object }));

            return content.Object;
        }

        #endregion
    }
}
