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
}
