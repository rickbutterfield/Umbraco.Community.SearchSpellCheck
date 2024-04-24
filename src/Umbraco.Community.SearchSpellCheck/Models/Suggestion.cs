namespace Umbraco.Community.SearchSpellCheck.Models
{
    public partial class Suggestion
    {
        public string? Word { get; set; }
        public int? Frequency { get; set; }
        public float? Priority { get; set; }
        public float? Jaro { get; set; }
        public float? Leven { get; set; }
        public float? NGram { get; set; }
    }
}
