using System;

namespace tkacheva_lr2.Models
{
    public class UserArticleState
    {
        public int Id { get; set; }

        public int AppUserId { get; set; }
        public AppUser? AppUser { get; set; }

        public int ArticleId { get; set; }
        public Article? Article { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
        public bool IsFavorite { get; set; } = false;
    }
}