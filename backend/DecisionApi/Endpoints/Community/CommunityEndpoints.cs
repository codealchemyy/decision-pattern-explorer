using DecisionApi.Database;
using Microsoft.EntityFrameworkCore;
using DecisionApi.Dtos.Community;
using System.Security.Claims;
using DecisionApi.Dtos;
using DecisionApi.Extensions;


namespace DecisionApi.Endpoints.Community;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        // Read-only for FR009 (no auth required)
        var group = app.MapGroup("/community");

        group.MapGet("/posts", GetPosts);
        group.MapGet("/posts/{id:guid}", GetPostById);
        group.MapGet("/posts/{id:guid}/comments", GetComments);
        group.MapPost("/posts/{id:guid}/comments", AddComment)
            .RequireAuthorization()
            .RequireRateLimiting("writes");

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

        private static async Task<IResult> GetPostById(Guid id, AppDbContext db)
        {
            var post = await db.CommunityPosts
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.Id == id && p.Visibility != Models.DecisionVisibility.Private)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Visibility,
                    p.CreatedAt,
                    AuthorDisplayName =
                        p.Visibility == Models.DecisionVisibility.PublicNickname ? p.AuthorDisplayName : null,
                    Category = p.Category == null ? null : new { p.Category.Id, p.Category.Name }
                })
                .FirstOrDefaultAsync();

            return post is null ? Results.NotFound() : Results.Ok(post);
        }

        private static async Task<IResult> GetComments(Guid id, AppDbContext db)
        {
            // Extra safety: if the post doesn't exist or is private -> 404
            var postExists = await db.CommunityPosts
                .AsNoTracking()
                .AnyAsync(p => p.Id == id && p.Visibility != Models.DecisionVisibility.Private);

            if (!postExists)
                return Results.NotFound();

            var comments = await db.Comments
                .AsNoTracking()
                .Where(c => c.CommunityPostId == id)
                .OrderBy(c => c.CreatedAt)
                .Join(
                    db.Users.AsNoTracking(),
                    c => c.UserId,
                    u => u.Id,
                    (c, u) => new
                    {
                        c.Id,
                        Text = c.Text,
                        c.CreatedAt,
                        AuthorDisplayName = u.DisplayName
                    }
                )
                .ToListAsync();

            return Results.Ok(comments);
        }

        private static async Task<IResult> AddComment(
            Guid id,
            CreateCommentRequest req,
            AppDbContext db,
            ClaimsPrincipal user)
        {
            var userId = user.GetUserId();

            if (string.IsNullOrWhiteSpace(req.Text))
                return ApiValidation.Problem(("text", "Text is required."));

            if (req.Text.Length > ValidationConstants.CommentTextMax)
                return ApiValidation.Problem(("text", $"Text must be at most {ValidationConstants.CommentTextMax} characters."));

            // Post must exist and must not be private
            var post = await db.CommunityPosts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.Visibility != Models.DecisionVisibility.Private);

            if (post is null)
                return Results.NotFound();

            var comment = new Models.Comment
            {
                CommunityPostId = id,
                UserId = userId,
                Text = req.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            db.Comments.Add(comment);
            await db.SaveChangesAsync();

            // Return shape including author display name
            var authorDisplayName = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.DisplayName)
                .FirstAsync();

            return Results.Created($"/community/posts/{id}/comments/{comment.Id}", new
            {
                comment.Id,
                Text = comment.Text,
                comment.CreatedAt,
                AuthorDisplayName = authorDisplayName
         });
    }
}

