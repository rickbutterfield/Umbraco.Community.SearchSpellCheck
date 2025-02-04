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
        private static bool _isReady;
        private static bool _isReadSet;
        private static object? _isReadyLock;
        private readonly IRuntimeState _runtimeState;
        private readonly IIndexRebuilder _indexRebuilder;
        private readonly SpellCheckOptions _spellCheckOptions;

        public BuildOnStartupHandler(
            IIndexRebuilder indexRebuilder,
            IRuntimeState runtimeState,
            IOptionsMonitor<SpellCheckOptions> optionsMonitor)
        {
            _indexRebuilder = indexRebuilder;
            _runtimeState = runtimeState;

            _spellCheckOptions = optionsMonitor.CurrentValue;
        }

        public void Handle(UmbracoRequestBeginNotification notification)
        {
            if (_runtimeState.Level != RuntimeLevel.Run)
            {
                return;
            }

            if (_spellCheckOptions.BuildOnStartup)
            {
                LazyInitializer.EnsureInitialized(
                    ref _isReady,
                    ref _isReadSet,
                    ref _isReadyLock,
                    () =>
                    {
                        if (_indexRebuilder.CanRebuild(_spellCheckOptions.IndexName))
                        {
                            _indexRebuilder.RebuildIndex(_spellCheckOptions.IndexName);
                        }

                        return true;
                    });
            }
        }
    }
}
