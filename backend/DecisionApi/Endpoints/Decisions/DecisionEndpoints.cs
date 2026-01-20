using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using DecisionApi.Database;
using DecisionApi.Extensions;
using Microsoft.EntityFrameworkCore;
using DecisionApi.Dtos.Decisions;


namespace DecisionApi.Endpoints.Decisions;

public static class DecisionEndpoints
{
    public static IEndpointRouteBuilder MapDecisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/decisions")
                       .RequireAuthorization(); // 🔒 all decision routes require JWT

        group.MapGet("/", ListMine);
        group.MapPost("/", CreateDecision);
        group.MapGet("/{id:guid}", GetById);
        group.MapGet("/{id:guid}/check-ins", GetCheckInsForDecision);

        /* group.MapPost("/", () => Results.StatusCode(StatusCodes.Status501NotImplemented));
        group.MapGet("/", () => Results.StatusCode(StatusCodes.Status501NotImplemented));
        group.MapGet("/{id:guid}", (Guid id) => Results.StatusCode(StatusCodes.Status501NotImplemented));*/
        return app;
    }
        private static async Task<IResult> ListMine(AppDbContext db, ClaimsPrincipal user)
        {
            var userId = user.GetUserId();

            var decisions = await db.Decisions
                .AsNoTracking()
                .Where(d => d.UserId == userId)
                .Include(d => d.Category)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    d.Notes,
                    d.MoodBefore,
                    d.Visibility,
                    d.CreatedAt,

                    Category = d.Category == null
                        ? null
                        : new { d.Category.Id, d.Category.Name },

                    LatestCheckInSummary = db.CheckIns
                        .Where(c => c.DecisionId == d.Id && c.UserId == userId)
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => new
                        {
                            c.MoodAfter,
                            c.Note,
                            c.CreatedAt
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Results.Ok(decisions);
        }

        private static async Task<IResult> CreateDecision(
            CreateDecisionRequest req,
            AppDbContext db,
            ClaimsPrincipal user)
        {
            var userId = user.GetUserId();

            // Basic validation (minimal, but real)
            if (string.IsNullOrWhiteSpace(req.Title))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["title"] = new[] { "Title is required." }
                });

            if (req.MoodBefore is < 1 or > 5)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["moodBefore"] = new[] { "MoodBefore must be between 1 and 5." }
                });

            var categoryExists = await db.Categories.AnyAsync(c => c.Id == req.CategoryId);
            if (!categoryExists)
                return Results.Problem(title: "Category not found", statusCode: StatusCodes.Status404NotFound);

            var decision = new Models.Decision
            {
                UserId = userId,
                CategoryId = req.CategoryId,
                Title = req.Title.Trim(),
                Notes = req.Notes,
                MoodBefore = req.MoodBefore,
                Visibility = req.Visibility,
                CreatedAt = DateTime.UtcNow
            };

            db.Decisions.Add(decision);
            await db.SaveChangesAsync();

            // Return with category shape (optional but nice for UI)
            var created = await db.Decisions
                .AsNoTracking()
                .Where(d => d.Id == decision.Id && d.UserId == userId)
                .Include(d => d.Category)
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    d.Notes,
                    d.MoodBefore,
                    d.Visibility,
                    d.CreatedAt,
                    Category = d.Category == null ? null : new { d.Category.Id, d.Category.Name },
                    LatestCheckInSummary = db.CheckIns
                        .Where(c => c.DecisionId == d.Id && c.UserId == userId)
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => new { c.MoodAfter, c.Note, c.CreatedAt })
                        .FirstOrDefault()

                })
                .FirstAsync();

            return Results.Created($"/decisions/{decision.Id}", created);
        }

        private static async Task<IResult> GetById(
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user)
        {
            var userId = user.GetUserId();

            var decision = await db.Decisions
                .AsNoTracking()
                .Where(d => d.Id == id && d.UserId == userId)
                .Include(d => d.Category)
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    d.Notes,
                    d.MoodBefore,
                    d.Visibility,
                    d.CreatedAt,
                    Category = d.Category == null ? null : new { d.Category.Id, d.Category.Name },
                    LatestCheckInSummary = db.CheckIns
                        .Where(c => c.DecisionId == d.Id && c.UserId == userId)
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => new { c.MoodAfter, c.Note, c.CreatedAt })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            return decision is null
                ? Results.NotFound()
                : Results.Ok(decision);
        }


        private static async Task<IResult> GetCheckInsForDecision(
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user)
        {
            var userId = user.GetUserId();

            // owner-only check (again hide others)
            var ownsDecision = await db.Decisions
                .AsNoTracking()
                .AnyAsync(d => d.Id == id && d.UserId == userId);

            if (!ownsDecision)
                return Results.NotFound();

            var checkIns = await db.CheckIns
                .AsNoTracking()
                .Where(c => c.DecisionId == id && c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.MoodAfter,
                    c.Note,
                    c.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(checkIns);
        }



}
