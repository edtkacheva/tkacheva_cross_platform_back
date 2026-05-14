using System.Text.Json.Serialization;

namespace tkacheva_lr2.Models
{
    public class ArticleCategory
    {
        public int Id { get; set; }

        public int ArticleId { get; set; }

        [JsonIgnore]
        public Article? Article { get; set; }

        public string Name { get; set; } = "";

        public string NormalizedName { get; set; } = "";
    }
}