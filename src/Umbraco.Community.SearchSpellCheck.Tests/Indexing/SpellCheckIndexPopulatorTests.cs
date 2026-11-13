using Examine;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Community.SearchSpellCheck.Indexing;

namespace Umbraco.Community.SearchSpellCheck.Tests.Indexing
{
    [TestFixture]
    public class SpellCheckIndexPopulatorTests
    {
        [Test]
        public void Indexes_Published_Content_And_Skips_Unpublished_Content_And_Everything_Beneath_It()
        {
            // 1 is live. 2 is unpublished, so 3 is not live even though it is flagged published itself.
            // 4 is live beneath 1, and 5 sits beneath 4 but is trashed.
            IReadOnlyList<string> indexed = Populate(
                Item(1, -1, published: true),
                Item(2, -1, published: false),
                Item(3, 2, published: true),
                Item(4, 1, published: true),
                Item(5, 4, published: true, trashed: true));

            Assert.That(indexed, Is.EquivalentTo(new[] { "1", "4" }));
        }

        [Test]
        public void Skips_Published_Items_In_The_Recycle_Bin()
        {
            // Trashing does not unpublish, and a trashed top level item has the recycle bin as its parent.
            IReadOnlyList<string> indexed = Populate(
                Item(1, -1, published: true),
                Item(2, -20, published: true, trashed: true),
                Item(3, 2, published: true, trashed: true));

            Assert.That(indexed, Is.EquivalentTo(new[] { "1" }));
        }

        [Test]
        public void A_Child_Is_Still_Skipped_When_Its_Parent_Was_Read_On_An_Earlier_Page()
        {
            // The page size is 500, so put the unpublished parent on the first page and the child on the second.
            IContent[] firstPage = Enumerable.Range(1, 500)
                .Select(i => Item(i, -1, published: i != 1))
                .ToArray();
            IContent child = Item(1000, 1, published: true);
            IContent liveChild = Item(1001, 2, published: true);

            var contentService = new Mock<IContentService>();
            contentService
                .Setup(x => x.GetPagedDescendants(-1, 0, 500, out It.Ref<long>.IsAny, It.IsAny<IQuery<IContent>?>(), It.IsAny<Ordering?>()))
                .Returns(firstPage);
            contentService
                .Setup(x => x.GetPagedDescendants(-1, 1, 500, out It.Ref<long>.IsAny, It.IsAny<IQuery<IContent>?>(), It.IsAny<Ordering?>()))
                .Returns(new[] { child, liveChild });

            IReadOnlyList<string> indexed = Run(contentService.Object);

            Assert.That(indexed, Does.Not.Contain("1").And.Not.Contain("1000"));
            Assert.That(indexed, Does.Contain("1001"));
        }

        private static IReadOnlyList<string> Populate(params IContent[] content)
        {
            var contentService = new Mock<IContentService>();
            contentService
                .Setup(x => x.GetPagedDescendants(-1, 0, 500, out It.Ref<long>.IsAny, It.IsAny<IQuery<IContent>?>(), It.IsAny<Ordering?>()))
                .Returns(content);

            return Run(contentService.Object);
        }

        private static IReadOnlyList<string> Run(IContentService contentService)
        {
            var indexed = new List<string>();
            var index = new Mock<IIndex>();
            index.SetupGet(x => x.Name).Returns("SpellCheckIndex");
            index
                .Setup(x => x.IndexItems(It.IsAny<IEnumerable<ValueSet>>()))
                .Callback((IEnumerable<ValueSet> sets) => indexed.AddRange(sets.Select(x => x.Id)));

            var populator = new SpellCheckIndexPopulator(TestValueSetBuilder.Create(), contentService);
            populator.RegisterIndex("SpellCheckIndex");
            populator.Populate(index.Object);

            return indexed;
        }

        private static IContent Item(int id, int parentId, bool published, bool trashed = false)
        {
            var content = new Mock<IContent>();
            content.SetupGet(x => x.Id).Returns(id);
            content.SetupGet(x => x.ParentId).Returns(parentId);
            content.SetupGet(x => x.Published).Returns(published);
            content.SetupGet(x => x.Trashed).Returns(trashed);
            return content.Object;
        }
    }
}
