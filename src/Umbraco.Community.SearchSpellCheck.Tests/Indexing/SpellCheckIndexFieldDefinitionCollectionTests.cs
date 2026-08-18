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

            Assert.That(definitions, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(definitions[0].Name, Is.EqualTo("word"));
                Assert.That(definitions[0].Type, Is.EqualTo(FieldDefinitionTypes.FullText));
            });
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
