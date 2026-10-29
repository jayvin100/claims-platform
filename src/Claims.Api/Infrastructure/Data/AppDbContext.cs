using Claims.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Infrastructure.Data;

/// <summary>The one database context shared by all modules (ADR-0001).</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DemoUser> Users => Set<DemoUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DemoUser>(user =>
        {
            user.ToTable("demo_users");
            user.Property(u => u.Name).HasMaxLength(100);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

            // Seed data lives in the migration, so applying migrations twice never duplicates users.
            user.HasData(DemoUser.Seed);
        });
    }
}
