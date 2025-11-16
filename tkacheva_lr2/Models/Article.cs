namespace tkacheva_lr2.Models
{
    public class Article
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Url { get; set; } = null!;
        public DateTime PublishedAt { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public string GetSummary(int maxLength = 100)
        {
            if (!string.IsNullOrWhiteSpace(Title))
                return Title.Length <= maxLength ? Title : Title.Substring(0, maxLength) + "...";
            return Url.Length <= maxLength ? Url : Url.Substring(0, maxLength) + "...";
        }

        public bool IsUrlValid()
        {
            return Uri.TryCreate(Url, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public void AssignCategory(int categoryId) => CategoryId = categoryId;
    }
}
