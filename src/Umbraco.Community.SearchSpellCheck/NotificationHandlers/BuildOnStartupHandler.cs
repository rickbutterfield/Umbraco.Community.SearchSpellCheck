using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;

namespace Umbraco.Community.SearchSpellCheck.NotificationHandlers
{
    public class BuildOnStartupHandler : INotificationHandler<UmbracoRequestBeginNotification>
    {
        private static bool _hasRebuilt;
        private static bool _hasRebuiltInitialised;
        private static object? _hasRebuiltLock;

        private readonly IRuntimeState _runtimeState;
        private readonly IIndexRebuilder _indexRebuilder;
        private readonly IOptionsMonitor<SpellCheckOptions> _optionsMonitor;
        private readonly ILogger<BuildOnStartupHandler> _logger;

        public BuildOnStartupHandler(
            IIndexRebuilder indexRebuilder,
            IRuntimeState runtimeState,
            IOptionsMonitor<SpellCheckOptions> optionsMonitor,
            ILogger<BuildOnStartupHandler> logger)
        {
            _indexRebuilder = indexRebuilder;
            _runtimeState = runtimeState;
            _optionsMonitor = optionsMonitor;
            _logger = logger;
        }

        public void Handle(UmbracoRequestBeginNotification notification)
        {
            if (_runtimeState.Level != RuntimeLevel.Run)
            {
                return;
            }

            SpellCheckOptions options = _optionsMonitor.CurrentValue;

            if (options.BuildOnStartup == false)
            {
                return;
            }

            // Runs once per application lifetime, on whichever request gets here first.
            LazyInitializer.EnsureInitialized(
                ref _hasRebuilt,
                ref _hasRebuiltInitialised,
                ref _hasRebuiltLock,
                () =>
                {
                    try
                    {
                        if (_indexRebuilder.CanRebuild(options.IndexName))
                        {
                            // Handle is a synchronous notification member, so the rebuild is fired without
                            // awaiting it. Any failure is observed and logged here instead of becoming an
                            // unobserved task exception.
                            _indexRebuilder.RebuildIndexAsync(options.IndexName).ContinueWith(
                                t => _logger.LogError(
                                    t.Exception,
                                    "Failed to build the spell check index {IndexName} on startup.",
                                    options.IndexName),
                                TaskContinuationOptions.OnlyOnFaulted);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Spell check index {IndexName} cannot be rebuilt, so it has not been populated on startup.",
                                options.IndexName);
                        }
                    }
                    catch (Exception ex)
                    {
                        // This runs on the first request to reach it. Letting it escape would fail that request
                        // for a reason the visitor has nothing to do with.
                        _logger.LogError(ex, "Failed to build the spell check index {IndexName} on startup.", options.IndexName);
                    }

                    return true;
                });
        }
    }
}
