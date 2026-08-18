using Examine;
using Moq;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Community.SearchSpellCheck.Indexing;

namespace Umbraco.Community.SearchSpellCheck.Tests.Indexing
{
    /// <summary>
    ///     Guards the boundary between this package's index and Umbraco's own.
    /// </summary>
    /// <remarks>
    ///     Umbraco decides which indexes it owns by type, not by name. Both of these assertions look pedantic but each
    ///     one, if broken, silently corrupts a live index rather than failing loudly.
    /// </remarks>
    [TestFixture]
    public class IndexOwnershipTests
    {
        [Test]
        public void SpellCheckIndex_Does_Not_Carry_The_Umbraco_Content_Index_Marker()
        {
            // ExamineUmbracoIndexingHandler pushes standard content documents into every
            // Indexes.OfType<IUmbracoContentIndex>() on publish, and ContentIndexPopulator and MediaIndexPopulator
            // both derive from IndexPopulator<IUmbracoContentIndex>. Any of those would overwrite our documents,
            // which are keyed by the same content ids but hold a "word" field the standard builder never produces.
            Assert.That(
                typeof(IUmbracoContentIndex).IsAssignableFrom(typeof(SpellCheckIndex)),
                Is.False,
                "SpellCheckIndex must not implement IUmbracoContentIndex, or Umbraco will write to it.");
        }

        [Test]
        public void SpellCheckIndex_Is_Still_Visible_To_Umbraco_As_An_Index()
        {
            // Dropping the content marker must not make the index invisible to the backoffice Examine dashboard,
            // which enumerates IUmbracoIndex.
            Assert.That(typeof(IUmbracoIndex).IsAssignableFrom(typeof(SpellCheckIndex)), Is.True);
        }

        [Test]
        public void Populator_Does_Not_Claim_Umbracos_Own_Indexes()
        {
            // IndexPopulator<TIndex> claims every index assignable to TIndex unless IsRegistered is overridden.
            // Umbraco's ExternalIndex and InternalIndex are both UmbracoContentIndex, so IUmbracoContentIndex is
            // what has to be rejected here. Without the override a backoffice rebuild of either would fill it
            // with spell check documents.
            SpellCheckIndexPopulator populator = CreatePopulator();
            var externalIndex = new Mock<IUmbracoContentIndex>();

            Assert.Multiple(() =>
            {
                Assert.That(populator.IsRegistered(externalIndex.Object), Is.False);
                Assert.That(populator.IsRegistered((IIndex)externalIndex.Object), Is.False);
            });
        }

        [Test]
        public void Populator_Does_Not_Claim_An_Arbitrary_Index()
        {
            SpellCheckIndexPopulator populator = CreatePopulator();
            var plainIndex = new Mock<IIndex>();

            Assert.That(populator.IsRegistered(plainIndex.Object), Is.False);
        }

        [Test]
        public void Populator_Still_Claims_An_Index_Registered_By_Name()
        {
            // The non-generic base also allows explicit registration by name, which the rebuild plumbing relies on.
            SpellCheckIndexPopulator populator = CreatePopulator();
            var namedIndex = new Mock<IIndex>();
            namedIndex.SetupGet(x => x.Name).Returns("SpellCheckIndex");

            populator.RegisterIndex("SpellCheckIndex");

            Assert.That(populator.IsRegistered(namedIndex.Object), Is.True);
        }

        /// <summary>
        ///     <see cref="SpellCheckIndexPopulator.IsRegistered(IUmbracoIndex)" /> is a pure predicate over the index
        ///     argument and never touches its collaborators, so there is nothing meaningful to substitute for them.
        /// </summary>
        private static SpellCheckIndexPopulator CreatePopulator() => new(null!, null!);
    }
}
