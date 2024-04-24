using Umbraco.Cms.Core.Manifest;

namespace Umbraco.Community.Umbraco.Community.SearchSpellCheck
{
    internal class SearchSpellCheckManifestFilter : IManifestFilter
    {
        public void Filter(List<PackageManifest> manifests)
        {
            var assembly = typeof(SearchSpellCheckManifestFilter).Assembly;

            manifests.Add(new PackageManifest
            {
                PackageName = "Umbraco.Community.SearchSpellCheck",
                Version = assembly.GetName()?.Version?.ToString(3) ?? "0.1.0",
                AllowPackageTelemetry = true,
                Scripts = new string[] {
                    // List any Script files
                    // Urls should start '/App_Plugins/Umbraco.Community.SearchSpellCheck/' not '/wwwroot/Umbraco.Community.SearchSpellCheck/', e.g.
                    // "/App_Plugins/Umbraco.Community.SearchSpellCheck/Scripts/scripts.js"
                },
                Stylesheets = new string[]
                {
                    // List any Stylesheet files
                    // Urls should start '/App_Plugins/Umbraco.Community.SearchSpellCheck/' not '/wwwroot/Umbraco.Community.SearchSpellCheck/', e.g.
                    // "/App_Plugins/Umbraco.Community.SearchSpellCheck/Styles/styles.css"
                }
            });
        }
    }
}
