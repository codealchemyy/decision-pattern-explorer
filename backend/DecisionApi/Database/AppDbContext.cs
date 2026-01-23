using DecisionApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DecisionApi.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<CommunityPost> CommunityPosts => Set<CommunityPost>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CheckIn>()
            .HasOne(c => c.Decision)
            .WithMany()
            .HasForeignKey(c => c.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CommunityPost>()
            .HasIndex(p => p.DecisionId)
            .IsUnique();
        
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.CommunityPost)
            .WithMany()
            .HasForeignKey(c => c.CommunityPostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);


    }


}
