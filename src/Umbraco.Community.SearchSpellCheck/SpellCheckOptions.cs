namespace Umbraco.Community.SearchSpellCheck
{
    public partial class SpellCheckOptions
    {
        public string IndexName { get; set; } = Constants.Configuration.DefaultIndexName;
        public List<string> IndexedFields { get; set; } = new List<string>(new string[] { "nodeName" });
        public bool AutoRebuildIndex { get; set; } = false;
        public int AutoRebuildDelay { get; set; } = 5;
        public int AutoRebuildRepeat { get; set; } = 30;
        public bool EnableLogging { get; set; } = false;
    }
}
