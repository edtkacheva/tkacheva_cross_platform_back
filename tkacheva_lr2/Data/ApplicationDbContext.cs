using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<AppUser> AppUsers { get; set; } = null!;
    }
}