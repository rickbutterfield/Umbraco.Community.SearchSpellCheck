using Umbraco.Cms.Core.Manifest;

namespace Umbraco.Community.SearchSpellCheck
{
    internal class SearchSpellCheckManifestFilter : IManifestFilter
    {
        public void Filter(List<PackageManifest> manifests)
        {
            var assembly = typeof(SearchSpellCheckManifestFilter).Assembly;

            manifests.Add(new PackageManifest
            {
                PackageName = "Umbraco.Community.SearchSpellCheck",
                Version = assembly.GetName().Version?.ToString(3) ?? "0.1.0",
                AllowPackageTelemetry = true
            });
        }
    }
}
