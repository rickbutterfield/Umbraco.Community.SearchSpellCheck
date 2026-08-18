namespace Umbraco.Community.SearchSpellCheck.Tests
{
    [TestFixture]
    public class SpellCheckOptionsTests
    {
        [Test]
        public void Defaults_Match_The_Documented_Configuration()
        {
            var options = new SpellCheckOptions();

            Assert.Multiple(() =>
            {
                Assert.That(options.IndexName, Is.EqualTo("SpellCheckIndex"));
                Assert.That(options.IndexedFields, Is.EqualTo(new[] { "nodeName" }));
                Assert.That(options.BuildOnStartup, Is.True);
                Assert.That(options.RebuildOnPublish, Is.True);
                Assert.That(options.EnableLogging, Is.False);
            });
        }

        [Test]
        public void IndexedFields_Is_Mutable_So_Configuration_Binding_Can_Populate_It()
        {
            // The configuration binder appends to the existing list rather than replacing it,
            // so the default must not be a fixed-size or read-only collection.
            var options = new SpellCheckOptions();

            Assert.DoesNotThrow(() => options.IndexedFields.Add("title"));
            Assert.That(options.IndexedFields, Does.Contain("title"));
        }
    }
}
