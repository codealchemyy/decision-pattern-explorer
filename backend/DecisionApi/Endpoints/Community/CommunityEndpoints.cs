using DecisionApi.Database;
using Microsoft.EntityFrameworkCore;

namespace DecisionApi.Endpoints.Community;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        // Read-only for FR009 (no auth required)
        var group = app.MapGroup("/community");

        group.MapGet("/posts", GetPosts);

        return app;
    }

    private static async Task<IResult> GetPosts(
        Guid? categoryId,
        AppDbContext db)
    {
        // Optional: validate category exists if categoryId is provided
        if (categoryId is not null)
        {
            var exists = await db.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Id == categoryId.Value);

            if (!exists)
                return Results.Problem(title: "Category not found", statusCode: StatusCodes.Status404NotFound);
        }

        // Feed: Only posts whose underlying Decision is NOT private.
        // Also never return userId/email.
        var query = db.CommunityPosts
            .AsNoTracking()
            .Include(p => p.Category)
            //.Include(p => p.Decision) // needed for privacy filter
            .Where(p => p.Visibility != Models.DecisionVisibility.Private);


        if (categoryId is not null)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Visibility,
                p.CreatedAt,
                AuthorDisplayName = p.Visibility == Models.DecisionVisibility.PublicNickname ? p.AuthorDisplayName : null,
                Category = p.Category == null ? null : new { p.Category.Id, p.Category.Name }
            })
            .ToListAsync();

        return Results.Ok(posts);
    }
}
