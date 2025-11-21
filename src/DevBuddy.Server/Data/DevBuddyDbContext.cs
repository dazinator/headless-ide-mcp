using Microsoft.EntityFrameworkCore;
using DevBuddy.Server.Data.Models;

namespace DevBuddy.Server.Data;

public class DevBuddyDbContext : DbContext
{
    public DevBuddyDbContext(DbContextOptions<DevBuddyDbContext> options) : base(options)
    {
    }

    public DbSet<GitRepositoryConfiguration> GitRepositories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GitRepositoryConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RemoteUrl).HasMaxLength(500);
            entity.Property(e => e.LocalPath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.CurrentBranch).HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }
}
