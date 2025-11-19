namespace tkacheva_lr2.Models
{
    public class RSSChannel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // название канала
        public string Url { get; set; } = string.Empty;   // URL RSS
        public string? Description { get; set; }

        // Навигация — список статей
        public List<Article> Articles { get; set; } = new();

        // ===== Бизнес-логика =====

        // Проверить, пустой ли канал
        public bool IsEmpty()
        {
            return Articles.Count == 0;
        }

        // Сравнение имени каналов без регистра
        public bool NameEquals(string other)
        {
            return Name.Equals(other, StringComparison.OrdinalIgnoreCase);
        }

        // Количество статей
        public int ArticleCount()
        {
            return Articles.Count;
        }

        // Короткое описание
        public string ShortDescription(int maxLen = 80)
        {
            if (string.IsNullOrWhiteSpace(Description))
                return $"RSS-channel: {Name}";

            return Description.Length <= maxLen ? Description : Description[..maxLen] + "...";
        }

        // Проверка валидности ссылки RSS
        public bool IsValidRssUrl()
        {
            return Uri.TryCreate(Url, UriKind.Absolute, out _);
        }
    }
}
