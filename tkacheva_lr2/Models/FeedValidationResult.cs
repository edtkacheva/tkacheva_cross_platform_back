namespace tkacheva_lr2.Models
{
    public class FeedValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FeedTitle { get; set; }
        public string? FeedDescription { get; set; }
        public List<FeedArticleDto> Articles { get; set; } = new();
    }

    public class FeedArticleDto
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string? Description { get; set; }
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    }
}