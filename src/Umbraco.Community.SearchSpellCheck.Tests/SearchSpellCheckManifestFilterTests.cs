using Umbraco.Cms.Core.Manifest;

namespace Umbraco.Community.SearchSpellCheck.Tests
{
    [TestFixture]
    public class SearchSpellCheckManifestFilterTests
    {
        [Test]
        public void Registers_The_Package_Under_Its_Own_Name()
        {
            var manifests = new List<PackageManifest>();

            new SearchSpellCheckManifestFilter().Filter(manifests);

            PackageManifest manifest = manifests.Single();
            Assert.Multiple(() =>
            {
                Assert.That(manifest.PackageName, Is.EqualTo("Umbraco.Community.SearchSpellCheck"));
                Assert.That(manifest.Version, Is.Not.Null.And.Not.Empty);
                Assert.That(manifest.AllowPackageTelemetry, Is.True);
            });
        }

        [Test]
        public void Leaves_Manifests_Registered_By_Other_Packages_Alone()
        {
            var manifests = new List<PackageManifest> { new() { PackageName = "Someone.Else" } };

            new SearchSpellCheckManifestFilter().Filter(manifests);

            Assert.That(manifests.Select(x => x.PackageName), Is.EqualTo(new[] { "Someone.Else", "Umbraco.Community.SearchSpellCheck" }));
        }

        [Test]
        public void Lives_In_The_Packages_Own_Namespace()
        {
            // It was declared in "Umbraco.Community.Umbraco.Community.SearchSpellCheck", a doubled-up namespace
            // left over from the package template.
            Assert.That(
                typeof(SearchSpellCheckManifestFilter).Namespace,
                Is.EqualTo("Umbraco.Community.SearchSpellCheck"));
        }
    }
}
