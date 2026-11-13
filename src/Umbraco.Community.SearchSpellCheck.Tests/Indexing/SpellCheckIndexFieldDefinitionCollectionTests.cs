using Examine;
using Umbraco.Community.SearchSpellCheck.Indexing;

namespace Umbraco.Community.SearchSpellCheck.Tests.Indexing
{
    [TestFixture]
    public class SpellCheckIndexFieldDefinitionCollectionTests
    {
        [Test]
        public void Defines_The_Word_Field_As_FullText()
        {
            // "word" is the field the Lucene spell checker builds its dictionary from.
            // Renaming it silently breaks every stored index, so pin the literal here.
            FieldDefinition[] definitions = SpellCheckIndexFieldDefinitionCollection.SpellCheckIndexFieldDefinitions;

            FieldDefinition word = definitions.Single(x => x.Name == "word");

            Assert.That(word.Type, Is.EqualTo(FieldDefinitionTypes.FullText));
        }

        [Test]
        public void Defines_The_Path_Field_As_Raw()
        {
            // SpellCheckIndex finds the descendants of a deleted item with a wildcard query on the path. A
            // tokenized path would split on the commas and the query would match nothing.
            FieldDefinition[] definitions = SpellCheckIndexFieldDefinitionCollection.SpellCheckIndexFieldDefinitions;

            FieldDefinition path = definitions.Single(x => x.Name == "__Path");

            Assert.That(path.Type, Is.EqualTo(FieldDefinitionTypes.Raw));
        }

        [Test]
        public void Collection_Exposes_The_Word_Field()
        {
            var collection = new SpellCheckIndexFieldDefinitionCollection();

            Assert.That(collection.TryGetValue("word", out FieldDefinition definition), Is.True);
            Assert.That(definition.Type, Is.EqualTo(FieldDefinitionTypes.FullText));
        }
    }
}
