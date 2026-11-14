using Examine;
using Umbraco.Cms.Infrastructure.Examine;

namespace Umbraco.Community.SearchSpellCheck.Indexing
{
    public class SpellCheckIndexFieldDefinitionCollection : FieldDefinitionCollection
    {
        public SpellCheckIndexFieldDefinitionCollection()
            : base(SpellCheckIndexFieldDefinitions)
        {
        }

        public static readonly FieldDefinition[] SpellCheckIndexFieldDefinitions =
        {
            new FieldDefinition(Constants.Internals.FieldName, FieldDefinitionTypes.FullText),

            // Kept as one untokenized term so SpellCheckIndex can find everything beneath an id by its path.
            new FieldDefinition(UmbracoExamineFieldNames.IndexPathFieldName, FieldDefinitionTypes.Raw)
        };
    }
}
