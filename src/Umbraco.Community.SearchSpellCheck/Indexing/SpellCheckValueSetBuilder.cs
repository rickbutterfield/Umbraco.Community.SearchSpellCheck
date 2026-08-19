using System.Text.Json;
using Examine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;
using static Umbraco.Cms.Core.Constants.PropertyEditors;

namespace Umbraco.Community.SearchSpellCheck.Indexing
{
    public class SpellCheckValueSetBuilder : BaseValueSetBuilder<IContent>
    {
        /// <summary>
        ///     Property editors we can pull readable words out of.
        /// </summary>
        private static readonly string[] SupportedPropertyEditors =
        {
            Aliases.TextBox,
            Aliases.TextArea,
            Aliases.RichText,
            Aliases.BlockList,
            Aliases.BlockGrid
        };

        private readonly UrlSegmentProviderCollection _urlSegmentProviders;
        private readonly PropertyEditorCollection _propertyEditors;
        private readonly IOptionsMonitor<SpellCheckOptions> _options;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IContentTypeService _contentTypeService;
        private readonly ILanguageService _languageService;
        private readonly ILogger<SpellCheckValueSetBuilder> _logger;
        private string? _defaultIsoCode;

        public SpellCheckValueSetBuilder(
            IOptionsMonitor<SpellCheckOptions> options,
            ILogger<SpellCheckValueSetBuilder> logger,
            UrlSegmentProviderCollection urlSegmentProviders,
            IShortStringHelper shortStringHelper,
            PropertyEditorCollection propertyEditors,
            IContentTypeService contentTypeService,
            ILanguageService languageService)
            : base(propertyEditors, true)
        {
            // Held as the monitor rather than a snapshot of CurrentValue. This is a singleton, so freezing the
            // options in the constructor meant an appsettings change never took effect until the site restarted,
            // which defeats the point of taking a monitor at all.
            _options = options;
            _logger = logger;
            _urlSegmentProviders = urlSegmentProviders;
            _shortStringHelper = shortStringHelper;
            _propertyEditors = propertyEditors;
            _contentTypeService = contentTypeService;
            _languageService = languageService;
        }

        /// <inheritdoc />
        public override IEnumerable<ValueSet> GetValueSets(params IContent[] content)
        {
            SpellCheckOptions options = _options.CurrentValue;
            IDictionary<Guid, IContentType> contentTypeDictionary = _contentTypeService.GetAll().ToDictionary(x => x.Key);

            if (options.EnableLogging)
            {
                _logger.LogInformation("Indexed fields: {IndexedFields}", string.Join(", ", options.IndexedFields));
            }

            foreach (IContent c in content)
            {
                var isVariant = c.ContentType.VariesByCulture();
                var availableCultures = new List<string>(c.AvailableCultures);
                if (availableCultures.Any() is false)
                {
                    availableCultures.Add(GetDefaultIsoCode());
                }

                List<IProperty> properties = SelectProperties(c, options);

                var indexValues = new Dictionary<string, object>
                {
                    ["id"] = c.Id,
                    [UmbracoExamineFieldNames.NodeKeyFieldName] = c.Key,
                    [UmbracoExamineFieldNames.NodeNameFieldName] = c.PublishName ?? c.Name ?? string.Empty,
                    ["urlName"] = c.GetUrlSegment(_shortStringHelper, _urlSegmentProviders) ?? string.Empty
                };

                if (isVariant)
                {
                    indexValues[UmbracoExamineFieldNames.VariesByCultureFieldName] = new object[] { "y" };

                    foreach (var culture in c.AvailableCultures)
                    {
                        // Field names are lower cased, matching how Umbraco's own ContentValueSetBuilder writes
                        // variant fields, but the culture itself is passed through in its original case where
                        // Umbraco does the same.
                        var lowerCulture = culture.ToLowerInvariant();

                        indexValues[$"urlName_{lowerCulture}"] =
                            c.GetUrlSegment(_shortStringHelper, _urlSegmentProviders, culture) ?? string.Empty;
                        indexValues[$"{UmbracoExamineFieldNames.NodeNameFieldName}_{lowerCulture}"] =
                            c.GetPublishName(culture) ?? string.Empty;
                        indexValues[$"{Constants.Internals.FieldName}_{lowerCulture}"] =
                            CollectCleanValues(properties, availableCultures, contentTypeDictionary, lowerCulture);
                    }
                }
                else
                {
                    indexValues[Constants.Internals.FieldName] =
                        CollectCleanValues(properties, availableCultures, contentTypeDictionary, null);
                }

                if (options.EnableLogging)
                {
                    _logger.LogInformation(
                        "Indexing content {ContentName} ({ContentId}) from properties {Properties}: {IndexValues}",
                        c.PublishName ?? c.Name,
                        c.Id,
                        string.Join(", ", properties.Select(x => x.Alias)),
                        JsonSerializer.Serialize(indexValues));
                }

                yield return new ValueSet(c.Id.ToInvariantString(), IndexTypes.Content, c.ContentType.Alias, indexValues);
            }
        }

        #region Private methods

        /// <summary>
        ///     Narrows a content item's properties down to the configured aliases that we can read words from.
        /// </summary>
        private List<IProperty> SelectProperties(IContent content, SpellCheckOptions options)
        {
            // Alias matching is case insensitive so that a mis-cased alias in appsettings still indexes rather than
            // silently producing an empty index.
            var configuredFields = new HashSet<string>(options.IndexedFields, StringComparer.OrdinalIgnoreCase);

            List<IProperty> properties = content.Properties
                .Where(x => configuredFields.Contains(x.Alias))
                .Where(x => SupportedPropertyEditors.Contains(x.PropertyType.PropertyEditorAlias))
                .ToList();

            if (options.EnableLogging && properties.Count == 0)
            {
                _logger.LogInformation(
                    "Content {ContentId} contributed no properties. Available aliases were {Available}, configured aliases are {Configured}.",
                    content.Id,
                    string.Join(", ", content.Properties.Select(x => x.Alias)),
                    string.Join(", ", options.IndexedFields));
            }

            return properties;
        }

        /// <summary>
        ///     The site's default language ISO code, cached after first use.
        /// </summary>
        /// <remarks>
        ///     <see cref="IValueSetBuilder{T}.GetValueSets" /> is a synchronous interface member, but
        ///     <see cref="ILanguageService" /> only exposes this asynchronously. The default language does not
        ///     change during the process lifetime, so caching after one blocking call is preferable to blocking on
        ///     every content item indexed.
        /// </remarks>
        private string GetDefaultIsoCode()
            => _defaultIsoCode ??= _languageService.GetDefaultIsoCodeAsync().GetAwaiter().GetResult();

        /// <summary>
        ///     Collects every distinct word-bearing value from <paramref name="properties" />.
        /// </summary>
        /// <remarks>
        ///     This previously kept a <c>Dictionary&lt;string, string&gt;</c> keyed by index field name and
        ///     <em>replaced</em> the entry when a key repeated. Umbraco's own <c>BaseValueSetBuilder.AddPropertyValue</c>,
        ///     which this was adapted from, appends instead:
        ///     <c>values[key] = new List&lt;object?&gt;(v) { val }.ToArray()</c>. Since a Block List or Block Grid
        ///     produces many values under the same key, all but the last were thrown away and never reached the
        ///     spelling dictionary. The key was then discarded anyway, so it is gone entirely.
        /// </remarks>
        private string CollectCleanValues(
            IEnumerable<IProperty> properties,
            IEnumerable<string> availableCultures,
            IDictionary<Guid, IContentType> contentTypeDictionary,
            string? culture)
        {
            var cleanValues = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IProperty property in properties)
            {
                IDataEditor? editor = _propertyEditors[property.PropertyType.PropertyEditorAlias];
                if (editor is null)
                {
                    continue;
                }

                IEnumerable<IndexValue> indexValues = editor.PropertyIndexValueFactory
                    .GetIndexValues(property, culture, null, PublishedValuesOnly, availableCultures, contentTypeDictionary);

                foreach (IndexValue indexValue in indexValues)
                {
                    if (indexValue.FieldName.IsNullOrWhiteSpace())
                    {
                        continue;
                    }

                    foreach (var value in indexValue.Values)
                    {
                        var text = value?.ToString();

                        if (text.IsNullOrWhiteSpace())
                        {
                            continue;
                        }

                        // Udis are identifiers, not words. This was previously only checked for values that
                        // arrived as strings, so a Udi surfacing as any other type went into the dictionary.
                        if (text!.StartsWith("umb://", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (seen.Add(text))
                        {
                            cleanValues.Add(text);
                        }
                    }
                }
            }

            return string.Join(" ", cleanValues);
        }

        #endregion
    }
}
