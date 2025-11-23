namespace tkacheva_lr2.Models
{
    public class Article
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }

        public int RSSChannelId { get; set; }
        public RSSChannel? RSSChannel { get; set; }

        public bool IsValidUrl()
        {
            return Uri.TryCreate(Url, UriKind.Absolute, out _);
        }

        public string ShortTitle()
        {
            return Title.Length <= 20 ? Title : Title[..20] + "...";
        }

        public void UpdatePublishDate()
        {
            PublishedAt = DateTime.UtcNow;
        }
    }
}
