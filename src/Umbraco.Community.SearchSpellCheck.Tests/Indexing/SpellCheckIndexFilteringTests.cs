using Examine;
using Examine.Search;
using Examine.Lucene;
using Examine.Lucene.Analyzers;
using Examine.Lucene.Directories;
using Examine.Lucene.Providers;
using Lucene.Net.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.SearchSpellCheck.Indexing;

namespace Umbraco.Community.SearchSpellCheck.Tests.Indexing
{
    /// <summary>
    ///     Runs value sets through a real in-memory Lucene <see cref="SpellCheckIndex" /> with Umbraco's published
    ///     content validator, which is what the package registers.
    /// </summary>
    [TestFixture]
    public class SpellCheckIndexFilteringTests
    {
        private const string ProtectedPath = "-1,10,11";

        private SpellCheckIndex _index = null!;
        private IIndex Index => _index;

        [SetUp]
        public void SetUp()
        {
            var publicAccess = new Mock<IPublicAccessService>();
            publicAccess
                .Setup(x => x.IsProtected(It.IsAny<string>()))
                .Returns((string path) => path.Contains(",10,")
                    ? Attempt.Succeed<PublicAccessEntry?>(null)
                    : Attempt.Fail<PublicAccessEntry?>());

            var options = new LuceneDirectoryIndexOptions
            {
                Analyzer = new CultureInvariantWhitespaceAnalyzer(),
                FieldDefinitions = new SpellCheckIndexFieldDefinitionCollection(),
                DirectoryFactory = new GenericDirectoryFactory(_ => new RAMDirectory()),
                Validator = new ContentValueSetValidator(true, false, publicAccess.Object, Mock.Of<IScopeProvider>())
            };

            var optionsMonitor = new Mock<IOptionsMonitor<LuceneDirectoryIndexOptions>>();
            optionsMonitor.Setup(x => x.Get("SpellCheckIndex")).Returns(options);

            var runtimeState = new Mock<IRuntimeState>();
            runtimeState.SetupGet(x => x.Level).Returns(RuntimeLevel.Run);

            _index = new SpellCheckIndex(
                NullLoggerFactory.Instance,
                "SpellCheckIndex",
                optionsMonitor.Object,
                Mock.Of<IHostingEnvironment>(),
                runtimeState.Object);
            _index.WithThreadingMode(IndexThreadingMode.Synchronous);
        }

        [TearDown]
        public void TearDown() => _index.Dispose();

        [Test]
        public void Indexes_A_Published_Item()
        {
            Index.IndexItems(new[] { ValueSetFor("1", "-1,1", "alpha") });

            Assert.That(IndexedIds(), Is.EquivalentTo(new[] { "1" }));
        }

        [Test]
        public void Does_Not_Index_An_Item_In_The_Recycle_Bin()
        {
            // Trashing does not unpublish, so the value set still says published. Only the path gives it away.
            Index.IndexItems(new[]
            {
                ValueSetFor("1", "-1,1", "alpha"),
                ValueSetFor("2", "-1,-20,2", "bravo")
            });

            Assert.That(IndexedIds(), Is.EquivalentTo(new[] { "1" }));
        }

        [Test]
        public void Does_Not_Index_Protected_Content()
        {
            Index.IndexItems(new[]
            {
                ValueSetFor("1", "-1,1", "alpha"),
                ValueSetFor("11", ProtectedPath, "secret")
            });

            Assert.That(IndexedIds(), Is.EquivalentTo(new[] { "1" }));
        }

        [Test]
        public void Removes_An_Indexed_Item_That_Moves_To_The_Recycle_Bin()
        {
            Index.IndexItems(new[] { ValueSetFor("1", "-1,1", "alpha"), ValueSetFor("2", "-1,2", "bravo") });
            Assert.That(IndexedIds(), Is.EquivalentTo(new[] { "1", "2" }));

            Index.IndexItems(new[] { ValueSetFor("2", "-1,-20,2", "bravo") });

            Assert.That(IndexedIds(), Is.EquivalentTo(new[] { "1" }));
        }

        [Test]
        public void Removes_An_Indexed_Item_That_Becomes_Protected()
        {
            Index.IndexItems(new[] { ValueSetFor("11", "-1,11", "secret") });
            Assert.That(IndexedIds(), Is.EquivalentTo(new[] { "11" }));

            Index.IndexItems(new[] { ValueSetFor("11", ProtectedPath, "secret") });

            Assert.That(IndexedIds(), Is.Empty);
        }

        [Test]
        public void Deleting_An_Item_Deletes_Its_Descendants_But_Not_Lookalike_Ids()
        {
            Index.IndexItems(new[]
            {
                ValueSetFor("5", "-1,5", "parent"),
                ValueSetFor("6", "-1,5,6", "child"),
                ValueSetFor("7", "-1,5,6,7", "grandchild"),
                ValueSetFor("55", "-1,55", "lookalike"),
                ValueSetFor("8", "-1,8,55,9", "lookalikeDeep"),
                ValueSetFor("9", "-1,9", "other")
            });

            Index.DeleteFromIndex("5");

            Assert.That(IndexedIds(), Is.EquivalentTo(new[] { "55", "8", "9" }));
        }

        [Test]
        public void Deleting_An_Item_Deletes_More_Than_A_Hundred_Descendants()
        {
            // Examine returns 100 hits by default, so an unpaged descendant lookup left the rest in the index.
            var valueSets = new List<ValueSet> { ValueSetFor("5", "-1,5", "parent"), ValueSetFor("55", "-1,55", "lookalike") };
            valueSets.AddRange(Enumerable.Range(1000, 250).Select(i => ValueSetFor(i.ToString(), $"-1,5,{i}", "child")));
            Index.IndexItems(valueSets);
            Assert.That(IndexedIds(), Has.Count.EqualTo(252));

            Index.DeleteFromIndex("5");

            Assert.That(IndexedIds(), Is.EquivalentTo(new[] { "55" }));
        }

        [Test]
        public void Does_Not_Index_Anything_That_Is_Not_Content()
        {
            var media = new ValueSet("3", IndexTypes.Media, "image", new Dictionary<string, object>
            {
                ["path"] = "-1,3",
                [UmbracoExamineFieldNames.PublishedFieldName] = "y",
                ["word"] = "picture"
            });

            Index.IndexItems(new[] { media });

            Assert.That(IndexedIds(), Is.Empty);
        }

        private static ValueSet ValueSetFor(string id, string path, string word)
            => new(id, IndexTypes.Content, "page", new Dictionary<string, object>
            {
                ["id"] = id,
                ["path"] = path,
                [UmbracoExamineFieldNames.PublishedFieldName] = "y",
                ["nodeName"] = word,
                ["word"] = word
            });

        private IReadOnlyList<string> IndexedIds()
        {
            _index.WaitForChanges();
            return _index.Searcher.CreateQuery().All().Execute(QueryOptions.SkipTake(0, 10000)).Select(x => x.Id).ToList();
        }
    }
}
