using Examine;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Changes;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Cms.Infrastructure.Search;
using Umbraco.Community.SearchSpellCheck.Indexing;
using Umbraco.Community.SearchSpellCheck.NotificationHandlers;

namespace Umbraco.Community.SearchSpellCheck.Tests.Indexing
{
    [TestFixture]
    public class RebuildOnPublishHandlerTests
    {
        private readonly List<string> _deleted = new();
        private readonly List<string> _indexed = new();
        private readonly Mock<IContentService> _contentService = new();
        private RebuildOnPublishHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _deleted.Clear();
            _indexed.Clear();

            var index = new Mock<IIndex>();
            index.Setup(x => x.DeleteFromIndex(It.IsAny<IEnumerable<string>>())).Callback((IEnumerable<string> ids) => _deleted.AddRange(ids));
            index
                .Setup(x => x.IndexItems(It.IsAny<IEnumerable<ValueSet>>()))
                .Callback((IEnumerable<ValueSet> sets) => _indexed.AddRange(sets.Select(x => x.Id)));

            IIndex? outIndex = index.Object;
            var examineManager = new Mock<IExamineManager>();
            examineManager.Setup(x => x.TryGetIndex(It.IsAny<string>(), out outIndex)).Returns(true);

            var runtimeState = new Mock<IRuntimeState>();
            runtimeState.SetupGet(x => x.Level).Returns(RuntimeLevel.Run);

            var indexingHandler = new Mock<IUmbracoIndexingHandler>();
            indexingHandler.SetupGet(x => x.Enabled).Returns(true);

            _handler = new RebuildOnPublishHandler(
                runtimeState.Object,
                indexingHandler.Object,
                examineManager.Object,
                _contentService.Object,
                TestValueSetBuilder.Create(),
                Mock.Of<IOptionsMonitor<SpellCheckOptions>>(x => x.CurrentValue == new SpellCheckOptions()),
                NullLogger<RebuildOnPublishHandler>.Instance);
        }

        [Test]
        public void Unpublishing_A_Page_Deletes_It_From_The_Index_And_Does_Not_Re_Add_Its_Descendants()
        {
            // The index deletes everything beneath a deleted id. The descendants are still flagged published,
            // so they must not be written back by this handler.
            IContent page = Item(1, -1, published: false);
            Setup(page, Item(2, 1, published: true));

            Handle(1, TreeChangeTypes.RefreshBranch);

            Assert.Multiple(() =>
            {
                Assert.That(_deleted, Is.EqualTo(new[] { "1" }));
                Assert.That(_indexed, Is.Empty);
            });
        }

        [Test]
        public void A_Published_Page_Beneath_An_Unpublished_Ancestor_Is_Deleted_Rather_Than_Indexed()
        {
            IContent page = Item(5, 2, published: true);
            Setup(page);
            _contentService.Setup(x => x.IsPathPublished(page)).Returns(false);

            Handle(5, TreeChangeTypes.RefreshNode);

            Assert.Multiple(() =>
            {
                Assert.That(_deleted, Is.EqualTo(new[] { "5" }));
                Assert.That(_indexed, Is.Empty);
            });
        }

        [Test]
        public void Publishing_A_Branch_Re_Adds_Its_Live_Descendants_Only()
        {
            // 1 is published. 2 is live, 3 is unpublished, 4 is beneath 3 so it is not live, 5 is trashed.
            IContent page = Item(1, -1, published: true);
            Setup(
                page,
                Item(2, 1, published: true),
                Item(3, 1, published: false),
                Item(4, 3, published: true),
                Item(5, 2, published: true, trashed: true));

            Handle(1, TreeChangeTypes.RefreshBranch);

            Assert.Multiple(() =>
            {
                Assert.That(_deleted, Is.Empty);
                Assert.That(_indexed, Is.EquivalentTo(new[] { "1", "2" }));
            });
        }

        [Test]
        public void Publishing_A_Single_Node_Does_Not_Touch_Its_Descendants()
        {
            IContent page = Item(1, -1, published: true);
            Setup(page, Item(2, 1, published: true));

            Handle(1, TreeChangeTypes.RefreshNode);

            Assert.That(_indexed, Is.EqualTo(new[] { "1" }));
        }

        [Test]
        public void Removing_A_Page_Deletes_It_From_The_Index()
        {
            Handle(7, TreeChangeTypes.Remove);

            Assert.That(_deleted, Is.EqualTo(new[] { "7" }));
        }

        private void Setup(IContent page, params IContent[] descendants)
        {
            _contentService.Setup(x => x.GetById(page.Id)).Returns(page);
            _contentService.Setup(x => x.IsPathPublished(page)).Returns(true);
            _contentService
                .Setup(x => x.GetPagedDescendants(page.Id, It.IsAny<long>(), It.IsAny<int>(), out It.Ref<long>.IsAny, It.IsAny<IQuery<IContent>?>(), It.IsAny<Ordering?>()))
                .Returns((int id, long pageIndex, int size, long total, IQuery<IContent>? filter, Ordering? ordering) =>
                    pageIndex == 0 ? descendants : Array.Empty<IContent>());
        }

        private void Handle(int id, TreeChangeTypes changeTypes)
        {
            var payloads = new[] { new ContentCacheRefresher.JsonPayload { Id = id, ChangeTypes = changeTypes } };
            _handler.Handle(new ContentCacheRefresherNotification(payloads, MessageType.RefreshByPayload));
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
