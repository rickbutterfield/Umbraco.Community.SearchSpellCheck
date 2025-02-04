namespace Umbraco.Community.SearchSpellCheck
{
    public partial class SpellCheckOptions
    {
        public string IndexName { get; set; } = Constants.Configuration.DefaultIndexName;
        public List<string> IndexedFields { get; set; } = new List<string>(new string[] { "nodeName" });
        public bool BuildOnStartup { get; set; } = true;
        public bool RebuildOnPublish { get; set; } = true;
        public bool EnableLogging { get; set; } = false;
    }
}
