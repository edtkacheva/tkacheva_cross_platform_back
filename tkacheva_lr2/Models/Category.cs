namespace tkacheva_lr2.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public ICollection<Article> Articles { get; set; } = new List<Article>();

        public void NormalizeName()
        {
            if (!string.IsNullOrWhiteSpace(Name))
                Name = Name.Trim();
        }

        public string ShortDescription(int maxLen = 80)
        {
            if (string.IsNullOrWhiteSpace(Description))
                return $"Категория: {Name}";
            return Description.Length <= maxLen ? Description : Description.Substring(0, maxLen) + "...";
        }
    }
}
