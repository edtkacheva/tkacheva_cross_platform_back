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
        public DbSet<Category> Categories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Связь Article -> Category (1 ко многим)
            modelBuilder.Entity<Article>()
                .HasOne(a => a.Category)
                .WithMany(c => c.Articles)
                .HasForeignKey(a => a.CategoryId);

            // начальные данные
            modelBuilder.Entity<AppUser>().HasData(
                new AppUser { Id = 1, UserName = "admin", Password = "admin" },
                new AppUser { Id = 2, UserName = "user", Password = "1234" }
            );

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Физика", Description = "Статьи по физике" },
                new Category { Id = 2, Name = "Химия", Description = "Статьи по химии" }
            );

            modelBuilder.Entity<Article>().HasData(
                new Article
                {
                    Id = 1,
                    Title = "Квантовая механика",
                    Url = "https://example.com/qm",
                    PublishedAt = DateTime.Now.AddDays(-10),
                    CategoryId = 1
                },
                new Article
                {
                    Id = 2,
                    Title = "Органическая химия",
                    Url = "https://example.com/organic",
                    PublishedAt = DateTime.Now.AddDays(-5),
                    CategoryId = 2
                }
            );
        }
    }
}
