using System.Text.Json.Serialization;

namespace tkacheva_lr2.Models
{
    public class ArticleKeyword
    {
        public int Id { get; set; }

        public int ArticleId { get; set; }

        [JsonIgnore]
        public Article? Article { get; set; }

        public string Text { get; set; } = "";

        public string NormalizedText { get; set; } = "";

        public string Source { get; set; } = "AI";

        public double Weight { get; set; } = 1.0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}