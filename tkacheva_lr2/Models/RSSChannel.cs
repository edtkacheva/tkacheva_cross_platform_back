namespace tkacheva_lr2.Models
{
    public class RSSChannel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<AppUser> Subscribers { get; set; } = new();

        public List<Article> Articles { get; set; } = new();

        public bool IsEmpty()
        {
            return Articles.Count == 0;
        }

        public bool NameEquals(string other)
        {
            return Name.Equals(other, StringComparison.OrdinalIgnoreCase);
        }

        public int ArticleCount()
        {
            return Articles.Count;
        }

        public string ShortDescription(int maxLen = 80)
        {
            if (string.IsNullOrWhiteSpace(Description))
                return $"RSS-channel: {Name}";

            return Description.Length <= maxLen ? Description : Description[..maxLen] + "...";
        }

        public bool IsValidRssUrl()
        {
            return Uri.TryCreate(Url, UriKind.Absolute, out _);
        }
    }
}
