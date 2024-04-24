using Examine;

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
            new FieldDefinition(Constants.Internals.FieldName, FieldDefinitionTypes.FullText)
        };
    }
}
