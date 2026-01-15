using Microsoft.EntityFrameworkCore;

namespace DecisionApi.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSets (tables) will be added in FR003 (Domain Models)
}
