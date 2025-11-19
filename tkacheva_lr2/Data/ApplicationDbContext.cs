using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Article> Articles { get; set; }
        public DbSet<RSSChannel> RSSChannels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Article → RSSChannel (1 ко многим)
            modelBuilder.Entity<Article>()
                .HasOne(a => a.RSSChannel)
                .WithMany(c => c.Articles)
                .HasForeignKey(a => a.RSSChannelId);

            // Users
            modelBuilder.Entity<AppUser>().HasData(
                new AppUser { Id = 1, UserName = "admin", Password = "admin" },
                new AppUser { Id = 2, UserName = "user", Password = "1234" }
            );

            // RSS Channels
            modelBuilder.Entity<RSSChannel>().HasData(
                new RSSChannel { Id = 1, Name = "Physics", Description = "Physics articles" },
                new RSSChannel { Id = 2, Name = "Chemistry", Description = "Articles on chemistry" }
            );

            // Articles
            modelBuilder.Entity<Article>().HasData(
                new Article
                {
                    Id = 1,
                    Title = "Quantum mechanics",
                    Url = "https://example.com/qm",
                    PublishedAt = new DateTime(2024, 1, 1),
                    RSSChannelId = 1
                },
                new Article
                {
                    Id = 2,
                    Title = "Organic chemistry",
                    Url = "https://example.com/organic",
                    PublishedAt = new DateTime(2025, 1, 1),
                    RSSChannelId = 2
                }
            );
        }
    }
}
