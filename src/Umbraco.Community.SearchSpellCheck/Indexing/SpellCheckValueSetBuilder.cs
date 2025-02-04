using Examine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Services;
using static Umbraco.Cms.Core.Constants.PropertyEditors;

namespace Umbraco.Community.SearchSpellCheck.Indexing
{
    public class SpellCheckValueSetBuilder : BaseValueSetBuilder<IContent>
    {
        private string[] SUPPORTED_FIELDS = new string[]
        {
            Aliases.TextBox,
            Aliases.TextArea,
            Aliases.TinyMce,
            Aliases.Grid,
            Aliases.BlockList,
            Aliases.BlockGrid
        };

        private readonly UrlSegmentProviderCollection _urlSegmentProviders;
        private readonly PropertyEditorCollection _propertyEditors;
        private readonly SpellCheckOptions _options;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IContentTypeService _contentTypeService;
        private readonly ILocalizationService _localizationService;

        private IEnumerable<string> _fields { get; set; }
        private ILogger<SpellCheckValueSetBuilder> _logger { get; set; }

        public SpellCheckValueSetBuilder(
            IOptionsMonitor<SpellCheckOptions> options,
            ILogger<SpellCheckValueSetBuilder> logger,
            UrlSegmentProviderCollection urlSegmentProviders,
            IShortStringHelper shortStringHelper,
            PropertyEditorCollection propertyEditors,
            IContentTypeService contentTypeService,
            ILocalizationService localizationService)
            : base(propertyEditors, true)
        {
            _options = options.CurrentValue;
            _fields = _options.IndexedFields;
            _logger = logger;
            _urlSegmentProviders = urlSegmentProviders;
            _shortStringHelper = shortStringHelper;
            _propertyEditors = propertyEditors;
            _contentTypeService = contentTypeService;
            _localizationService = localizationService;

            if (_options.EnableLogging)
            {
                _logger.LogInformation("Indexed fields: {0}", string.Join(", ", _fields));
            }
        }

        /// <inheritdoc />
        public override IEnumerable<ValueSet> GetValueSets(params IContent[] content)
        {
            IDictionary<Guid, IContentType> contentTypeDictionary = _contentTypeService.GetAll().ToDictionary(x => x.Key);

            foreach (var c in content)
            {
                var isVariant = c.ContentType.VariesByCulture();
                var availableCultures = new List<string>(c.AvailableCultures);
                if (availableCultures.Any() is false)
                {
                    availableCultures.Add(_localizationService.GetDefaultLanguageIsoCode());
                }
                var urlValue = c.GetUrlSegment(_shortStringHelper, _urlSegmentProviders);
                var properties = c.Properties.ToList();

                if (_options.EnableLogging)
                {
                    _logger.LogInformation("Properties: ", string.Join(", ", properties.Select(x => x.Alias)));
                }

                properties = properties.Where(x => _fields.Contains(x.Alias)).ToList();

                if (_options.EnableLogging)
                {
                    _logger.LogInformation("Properties filtered by IndexedFields: ", string.Join(", ", properties.Select(x => x.Alias)));
                }

                properties = properties.Where(x => SUPPORTED_FIELDS.Contains(x.PropertyType.PropertyEditorAlias)).ToList();

                if (_options.EnableLogging)
                {
                    _logger.LogInformation("Properties filtered by SUPPORTED_FIELDS: ", string.Join(", ", properties.Select(x => x.Alias)));
                }

                if (_options.EnableLogging)
                {
                    _logger.LogInformation("Indexing content {0} ({1})", c.PublishName ?? c.Name, c.Id);
                    _logger.LogInformation("Properties to be indexed: {0}", string.Join(", ", properties.Select(x => x.Alias)));
                }

                var indexValues = new Dictionary<string, object>()
                {
                    ["id"] = c.Id,
                    [UmbracoExamineFieldNames.NodeKeyFieldName] = c.Key,
                    [UmbracoExamineFieldNames.NodeNameFieldName] = c.PublishName ?? c.Name,
                    ["urlName"] = urlValue
                };

                if (isVariant)
                {
                    indexValues[UmbracoExamineFieldNames.VariesByCultureFieldName] = new object[] { "y" };

                    foreach (var culture in c.AvailableCultures)
                    {
                        var lowerCulture = culture.ToLowerInvariant();
                        var variantUrl = c.GetUrlSegment(_shortStringHelper, _urlSegmentProviders, culture);
                        indexValues[$"urlName_{lowerCulture}"] = variantUrl;
                        indexValues[$"nodeName_{lowerCulture}"] = c.GetPublishName(lowerCulture);
                        indexValues[$"{Constants.Internals.FieldName}_{lowerCulture}"] = CollectCleanValues(properties, availableCultures, contentTypeDictionary, culture.ToLowerInvariant());
                    }
                }
                else
                {
                    indexValues[Constants.Internals.FieldName] = CollectCleanValues(properties, availableCultures, contentTypeDictionary, null);
                }

                if (_options.EnableLogging)
                {
                    _logger.LogInformation("Index values: {0}", JsonConvert.SerializeObject(indexValues));
                }

                var vs = new ValueSet(c.Id.ToInvariantString(), IndexTypes.Content, c.ContentType.Alias, indexValues);

                yield return vs;
            }
        }

        #region Private methods
        /// <summary>
        /// Collect clean values from a list of <see cref="Property"/> values
        /// </summary>
        /// <param name="properties">Properties to be checked</param>
        /// <param name="cleanValues">List of clean values to be output</param>
        private string CollectCleanValues(IEnumerable<IProperty> properties, List<string>? availableCultures, IDictionary<Guid, IContentType> contentTypeDictionary, string? culture = null)
        {
            List<string> cleanValues = new();
            Dictionary<string, string>? values = new();

            foreach (var property in properties)
            {
                var editor = _propertyEditors[property.PropertyType.PropertyEditorAlias];
                var indexVals = editor?.PropertyIndexValueFactory.GetIndexValues(property, culture, null, true, availableCultures, contentTypeDictionary);

                if (property.PropertyType.PropertyEditorAlias == Aliases.BlockGrid || property.PropertyType.PropertyEditorAlias == Aliases.BlockList)
                {
                    _ = indexVals;
                }

                if (indexVals != null)
                {
                    foreach (KeyValuePair<string, IEnumerable<object?>> keyVal in indexVals)
                    {
                        if (keyVal.Key.IsNullOrWhiteSpace())
                        {
                            continue;
                        }

                        var cultureSuffix = culture == null ? string.Empty : "_" + culture;

                        foreach (var val in keyVal.Value)
                        {
                            switch (val)
                            {
                                // only add the value if its not null or empty (we'll check for string explicitly here too)
                                case null:
                                    continue;
                                case string strVal:
                                {
                                    if (strVal.IsNullOrWhiteSpace())
                                    {
                                        continue;
                                    }

                                    if (strVal.StartsWith("umb://"))
                                    {
                                        continue;
                                    }

                                    var key = $"{keyVal.Key}{cultureSuffix}";
                                    if (values?.TryGetValue(key, out string? v) ?? false)
                                    {
                                        values[key] = val.ToString();
                                    }
                                    else
                                    {
                                        values?.Add($"{keyVal.Key}{cultureSuffix}", val.ToString());
                                    }
                                }

                                break;
                                default:
                                {
                                    var key = $"{keyVal.Key}{cultureSuffix}";
                                    if (values?.TryGetValue(key, out string? v) ?? false)
                                    {
                                        values[key] = val.ToString();
                                    }
                                    else
                                    {
                                        values?.Add($"{keyVal.Key}{cultureSuffix}", val.ToString() );
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }

            cleanValues = values?.Select(x => x.Value).ToList();
            cleanValues = cleanValues.Distinct().ToList();
            return string.Join(" ", cleanValues);
        }
#endregion
    }
}
