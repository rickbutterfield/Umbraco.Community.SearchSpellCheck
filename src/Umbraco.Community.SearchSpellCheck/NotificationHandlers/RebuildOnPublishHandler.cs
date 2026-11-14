using Examine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Changes;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure;
using Umbraco.Cms.Infrastructure.Search;
using Umbraco.Community.SearchSpellCheck.Indexing;
using Umbraco.Extensions;

namespace Umbraco.Community.SearchSpellCheck.NotificationHandlers
{
    public class RebuildOnPublishHandler : INotificationHandler<ContentCacheRefresherNotification>
    {
        private readonly IRuntimeState _runtimeState;
        private readonly IUmbracoIndexingHandler _umbracoIndexingHandler;
        private readonly IExamineManager _examineManager;
        private readonly IContentService _contentService;
        private readonly SpellCheckValueSetBuilder _spellCheckValueSetBuilder;
        private readonly IOptionsMonitor<SpellCheckOptions> _optionsMonitor;
        private readonly ILogger<RebuildOnPublishHandler> _logger;

        public RebuildOnPublishHandler(
            IRuntimeState runtimeState,
            IUmbracoIndexingHandler umbracoIndexingHandler,
            IExamineManager examineManager,
            IContentService contentService,
            SpellCheckValueSetBuilder spellCheckValueSetBuilder,
            IOptionsMonitor<SpellCheckOptions> optionsMonitor,
            ILogger<RebuildOnPublishHandler> logger)
        {
            _runtimeState = runtimeState;
            _umbracoIndexingHandler = umbracoIndexingHandler;
            _examineManager = examineManager;
            _contentService = contentService;
            _spellCheckValueSetBuilder = spellCheckValueSetBuilder;
            _optionsMonitor = optionsMonitor;
            _logger = logger;
        }

        /// <summary>
        ///     Updates the index based on content changes.
        /// </summary>
        public void Handle(ContentCacheRefresherNotification notification)
        {
            if (NotificationHandlingIsDisabled())
            {
                return;
            }

            var indexName = _optionsMonitor.CurrentValue.IndexName;

            if (!_examineManager.TryGetIndex(indexName, out IIndex? index))
            {
                // Previously this threw. It runs inside a publish, so a missing or misnamed index took the
                // editor's publish down with it rather than just leaving suggestions stale.
                _logger.LogWarning(
                    "Spell check index {IndexName} was not found, so it has not been updated for this change.",
                    indexName);
                return;
            }

            ContentCacheRefresher.JsonPayload[] payloads = GetNotificationPayloads(notification);

            foreach (ContentCacheRefresher.JsonPayload payload in payloads)
            {
                if (payload.Blueprint)
                {
                    continue;
                }

                if (payload.ChangeTypes.HasType(TreeChangeTypes.Remove))
                {
                    // The index deletes everything beneath the id as well.
                    index.DeleteFromIndex(payload.Id.ToString());
                }
                else if (payload.ChangeTypes.HasType(TreeChangeTypes.RefreshNode) ||
                         payload.ChangeTypes.HasType(TreeChangeTypes.RefreshBranch))
                {
                    IContent? content = _contentService.GetById(payload.Id);
                    if (content == null || content.Trashed || !content.Published || !_contentService.IsPathPublished(content))
                    {
                        // Not live, so neither is anything beneath it. Deleting the id deletes the branch.
                        index.DeleteFromIndex(payload.Id.ToString());
                        continue;
                    }

                    index.IndexItems(_spellCheckValueSetBuilder.GetValueSets(content));

                    if (payload.ChangeTypes.HasType(TreeChangeTypes.RefreshBranch))
                    {
                        IndexPublishedDescendants(index, content);
                    }
                }
            }
        }

        /// <summary>
        ///     Re-adds the live descendants of a node, which were removed if it was ever unpublished.
        /// </summary>
        private void IndexPublishedDescendants(IIndex index, IContent content)
        {
            const int pageSize = 500;
            var page = 0;
            var total = long.MaxValue;

            // The node itself is published, and so is everything above it. Ordering by path means a parent is
            // always seen before its children, so an item is live when it is published and its parent is live.
            var live = new HashSet<int> { content.Id };

            while (page * pageSize < total)
            {
                IContent[] descendants = _contentService
                    .GetPagedDescendants(content.Id, page++, pageSize, out total, ordering: Ordering.By("Path"))
                    .ToArray();

                var liveDescendants = new List<IContent>();
                foreach (IContent descendant in descendants)
                {
                    if (descendant.Published && !descendant.Trashed && live.Contains(descendant.ParentId))
                    {
                        live.Add(descendant.Id);
                        liveDescendants.Add(descendant);
                    }
                }

                if (liveDescendants.Count > 0)
                {
                    index.IndexItems(_spellCheckValueSetBuilder.GetValueSets(liveDescendants.ToArray()));
                }
            }
        }

        private bool NotificationHandlingIsDisabled()
        {
            // Only handle events when the site is running.
            if (_runtimeState.Level != RuntimeLevel.Run)
            {
                return true;
            }

            if (_umbracoIndexingHandler.Enabled == false)
            {
                return true;
            }

            if (Suspendable.ExamineEvents.CanIndex == false)
            {
                return true;
            }

            if (_optionsMonitor.CurrentValue.RebuildOnPublish == false)
            {
                return true;
            }

            return false;
        }

        private ContentCacheRefresher.JsonPayload[] GetNotificationPayloads(CacheRefresherNotification notification)
        {
            if (notification.MessageType != MessageType.RefreshByPayload ||
                notification.MessageObject is not ContentCacheRefresher.JsonPayload[] payloads)
            {
                throw new NotSupportedException();
            }

            return payloads;
        }
    }
}
