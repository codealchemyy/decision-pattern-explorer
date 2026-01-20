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
        group.MapPatch("/{id:guid}", UpdateDecision);
        group.MapDelete("/{id:guid}", DeleteDecision);


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

        private static async Task<IResult> UpdateDecision(
            Guid id,
            UpdateDecisionRequest req,
            AppDbContext db,
            ClaimsPrincipal user)
        {
            var userId = user.GetUserId();

            var decision = await db.Decisions
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (decision is null)
                return Results.NotFound();

            // Validation
            if (req.Title is not null && string.IsNullOrWhiteSpace(req.Title))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["title"] = new[] { "Title cannot be empty." }
                });

            if (req.Title is not null && req.Title.Length > 1000)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["title"] = new[] { "Title must be at most 1000 characters." }
                });

            if (req.MoodBefore is not null && (req.MoodBefore < 1 || req.MoodBefore > 5))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["moodBefore"] = new[] { "MoodBefore must be between 1 and 5." }
                });

            if (req.Notes is not null && req.Notes.Length > 2000)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["notes"] = new[] { "Notes must be at most 2000 characters." }
                });

            // Apply
            if (req.CategoryId is not null)
            {
                var exists = await db.Categories.AnyAsync(c => c.Id == req.CategoryId.Value);
                if (!exists)
                    return Results.Problem(title: "Category not found", statusCode: StatusCodes.Status404NotFound);

                decision.CategoryId = req.CategoryId.Value;
            }

            if (req.Title is not null) decision.Title = req.Title.Trim();
            if (req.MoodBefore is not null) decision.MoodBefore = req.MoodBefore.Value;
            if (req.Visibility is not null) decision.Visibility = req.Visibility.Value;

            // allow clearing notes via empty string
            if (req.Notes is not null)
                decision.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes;

            await db.SaveChangesAsync();

            // Return updated shape like GET by id
            var updated = await db.Decisions
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
                .FirstAsync();

            return Results.Ok(updated);
        }

        private static async Task<IResult> DeleteDecision(
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user)
        {
            var userId = user.GetUserId();

            var decision = await db.Decisions
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (decision is null)
                return Results.NotFound();

            // Delete rule: allow delete even if shared publicly (we’ll handle community later)
            db.Decisions.Remove(decision);
            await db.SaveChangesAsync();

            return Results.NoContent();
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
