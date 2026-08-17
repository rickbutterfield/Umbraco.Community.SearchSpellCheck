using Examine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
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
                if (payload.ChangeTypes.HasType(TreeChangeTypes.Remove))
                {
                    index.DeleteFromIndex(payload.Id.ToString());
                }
                else if (payload.ChangeTypes.HasType(TreeChangeTypes.RefreshNode) ||
                         payload.ChangeTypes.HasType(TreeChangeTypes.RefreshBranch))
                {
                    IContent? content = _contentService.GetById(payload.Id);
                    if (content == null || content.Trashed)
                    {
                        index.DeleteFromIndex(payload.Id.ToString());
                        continue;
                    }

                    IEnumerable<ValueSet> valueSets = _spellCheckValueSetBuilder.GetValueSets(content);
                    index.IndexItems(valueSets);
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
