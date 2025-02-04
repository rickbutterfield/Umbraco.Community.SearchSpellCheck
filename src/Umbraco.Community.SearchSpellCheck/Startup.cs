using Examine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Community.SearchSpellCheck.Indexing;
using Umbraco.Community.SearchSpellCheck.Interfaces;
using Umbraco.Community.SearchSpellCheck.NotificationHandlers;
using Umbraco.Community.SearchSpellCheck.Services;
using Umbraco.Community.Umbraco.Community.SearchSpellCheck;

namespace Umbraco.Community.SearchSpellCheck
{
    public class Startup : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.ManifestFilters().Append<SearchSpellCheckManifestFilter>();

            // Configuration
            builder.Services.Configure<SpellCheckOptions>(builder.Config.GetSection(Constants.Configuration.ConfigurationSection));
            var options = builder.Config.GetSection(Constants.Configuration.ConfigurationSection).Get<SpellCheckOptions>() ?? new SpellCheckOptions();

            // Indexes
            builder.Services
                .AddExamineLuceneIndex<SpellCheckIndex, ConfigurationEnabledDirectoryFactory>(options.IndexName)
                .ConfigureOptions<SpellCheckIndexOptions>();

            builder
                .AddNotificationHandler<UmbracoRequestBeginNotification, BuildOnStartupHandler>()
                .AddNotificationHandler<ContentCacheRefresherNotification, RebuildOnPublishHandler>();

            builder.Services.AddSingleton<SpellCheckValueSetBuilder>();
            builder.Services.AddSingleton<IIndexPopulator, SpellCheckIndexPopulator>();

            // Services
            builder.Services.AddSingleton<ISuggestionService, SuggestionService>();
        }
    }
}
