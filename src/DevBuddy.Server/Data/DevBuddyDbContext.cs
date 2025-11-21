using Microsoft.EntityFrameworkCore;
using DevBuddy.Server.Data.Models;

namespace DevBuddy.Server.Data;

public class DevBuddyDbContext : DbContext
{
    public DevBuddyDbContext(DbContextOptions<DevBuddyDbContext> options) : base(options)
    {
    }

    public DbSet<GitRepositoryConfiguration> GitRepositories { get; set; }
    
    // Context Graph entities
    public DbSet<Domain> Domains { get; set; }
    public DbSet<NodeType> NodeTypes { get; set; }
    public DbSet<Node> Nodes { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<EdgeType> EdgeTypes { get; set; }
    public DbSet<Edge> Edges { get; set; }

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

        // Context Graph configuration
        modelBuilder.Entity<Domain>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasOne(e => e.ParentDomain)
                .WithMany(e => e.ChildDomains)
                .HasForeignKey(e => e.ParentDomainId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NodeType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasOne(e => e.Domain)
                .WithMany(e => e.NodeTypes)
                .HasForeignKey(e => e.DomainId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Node>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MetadataJson);
            entity.Property(e => e.ContentStorageType).IsRequired();
            entity.Property(e => e.ExternalUri).HasMaxLength(1000);
            entity.HasOne(e => e.Domain)
                .WithMany(e => e.Nodes)
                .HasForeignKey(e => e.DomainId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.NodeType)
                .WithMany(e => e.Nodes)
                .HasForeignKey(e => e.NodeTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Node)
                .WithOne(e => e.Document)
                .HasForeignKey<Document>(e => e.NodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EdgeType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<Edge>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetadataJson);
            entity.HasOne(e => e.FromNode)
                .WithMany(e => e.OutgoingEdges)
                .HasForeignKey(e => e.FromNodeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ToNode)
                .WithMany(e => e.IncomingEdges)
                .HasForeignKey(e => e.ToNodeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.EdgeType)
                .WithMany(e => e.Edges)
                .HasForeignKey(e => e.EdgeTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
