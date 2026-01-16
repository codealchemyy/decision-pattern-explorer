using DecisionApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DecisionApi.Database;

public static class SeedData
{
    public static async Task EnsureSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure DB is on latest migrations (nice for teammates)
        await db.Database.MigrateAsync();

        // 1) Categories
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Id = Guid.NewGuid(), Name = "Career" },
                new Category { Id = Guid.NewGuid(), Name = "Health" },
                new Category { Id = Guid.NewGuid(), Name = "Relationships" }
            );
            await db.SaveChangesAsync();
        }

        // 2) User (DEV ONLY)
        User? user = await db.Users.FirstOrDefaultAsync(u => u.Email == "demo@local");
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = "demo@local",
                DisplayName = "Demo User",
                PasswordHash = "DEV_ONLY_NOT_A_REAL_HASH",
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        // 3) Decisions (+ CheckIns, CommunityPost, Comment)
        if (!await db.Decisions.AnyAsync())
        {
            var career = await db.Categories.FirstAsync(c => c.Name == "Career");
            var health = await db.Categories.FirstAsync(c => c.Name == "Health");

            var d1 = new Decision
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CategoryId = career.Id,
                Title = "Apply to Mercedes internship",
                Notes = "Focus on Digital Twin keywords, keep it concise.",
                MoodBefore = 3,
                Visibility = DecisionVisibility.Anonymous,
                CreatedAt = DateTime.UtcNow
            };

            var d2 = new Decision
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CategoryId = health.Id,
                Title = "Start 10-min daily mobility routine",
                Notes = "Short, consistent, no pressure.",
                MoodBefore = 4,
                Visibility = DecisionVisibility.Private,
                CreatedAt = DateTime.UtcNow
            };

            db.Decisions.AddRange(d1, d2);
            await db.SaveChangesAsync();

            db.CheckIns.AddRange(
                new CheckIn { Id = Guid.NewGuid(), DecisionId = d1.Id, UserId = user.Id, MoodAfter = 4, Note = "Felt clearer after writing bullet points.", CreatedAt = DateTime.UtcNow },
                new CheckIn { Id = Guid.NewGuid(), DecisionId = d2.Id, UserId = user.Id, MoodAfter = 5, Note = "Body feels lighter.", CreatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();

            var post = new CommunityPost
            {
                Id = Guid.NewGuid(),
                DecisionId = d1.Id,
                CategoryId = career.Id,
                Title = d1.Title,
                Visibility = DecisionVisibility.Anonymous,
                CreatedAt = DateTime.UtcNow
            };
            db.CommunityPosts.Add(post);
            await db.SaveChangesAsync();

            db.Comments.Add(new Comment
            {
                Id = Guid.NewGuid(),
                CommunityPostId = post.Id,
                UserId = user.Id,
                Text = "Small steps, big momentum. You’ve got this.",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}
