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
        public DbSet<UserArticleState> UserArticleStates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>()
                .HasMany(u => u.SubscribedChannels)
                .WithMany(c => c.Subscribers)
                .UsingEntity(j => j.ToTable("UserChannelSubscriptions"));

            // Article → RSSChannel
            modelBuilder.Entity<Article>()
                .HasOne(a => a.RSSChannel)
                .WithMany(c => c.Articles)
                .HasForeignKey(a => a.RSSChannelId);

            // Users
            modelBuilder.Entity<AppUser>().HasData(
                new AppUser { Id = 1, UserName = "admin", Password = "longpasswordforadmin" }
            );



            modelBuilder.Entity<RSSChannel>()
                .HasIndex(c => c.Url)
                .IsUnique();

            

            modelBuilder.Entity<Article>()
              .HasOne(a => a.RSSChannel)
              .WithMany(c => c.Articles)
              .HasForeignKey(a => a.RSSChannelId)
              .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserArticleState>()
                .HasOne(x => x.AppUser)
                .WithMany(u => u.ArticleStates)
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

                        modelBuilder.Entity<UserArticleState>()
                            .HasOne(x => x.Article)
                            .WithMany(a => a.UserStates)
                            .HasForeignKey(x => x.ArticleId)
                            .OnDelete(DeleteBehavior.Cascade);

                        modelBuilder.Entity<UserArticleState>()
                            .HasIndex(x => new { x.AppUserId, x.ArticleId })
                            .IsUnique();
        }
    }
}
