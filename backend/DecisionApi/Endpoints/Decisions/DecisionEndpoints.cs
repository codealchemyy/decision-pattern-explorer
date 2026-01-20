using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using DecisionApi.Database;
using DecisionApi.Extensions;
using Microsoft.EntityFrameworkCore;
using DecisionApi.Dtos.Decisions;
using DecisionApi.Dtos;


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

            var categoryExists = await db.Categories.AnyAsync(c => c.Id == req.CategoryId);
            if (!categoryExists)
                return Results.Problem(title: "Category not found", statusCode: StatusCodes.Status404NotFound);
            
            if (string.IsNullOrWhiteSpace(req.Title))
                return ApiValidation.Problem(("title", "Title is required."));

            if (req.Title.Length > ValidationConstants.DecisionTitleMax)
                return ApiValidation.Problem(("title", $"Title must be at most {ValidationConstants.DecisionTitleMax} characters."));

            if (req.MoodBefore is < ValidationConstants.MoodMin or > ValidationConstants.MoodMax)
                return ApiValidation.Problem(("moodBefore", "MoodBefore must be between 1 and 5."));

            if (req.Notes is not null && req.Notes.Length > ValidationConstants.DecisionNotesMax)
                return ApiValidation.Problem(("notes", $"Notes must be at most {ValidationConstants.DecisionNotesMax} characters."));


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

            // validation (centralized)
            if (req.Title is not null && string.IsNullOrWhiteSpace(req.Title))
                return ApiValidation.Problem(("title", "Title cannot be empty."));

            if (req.Title is not null && req.Title.Length > ValidationConstants.DecisionTitleMax)
                return ApiValidation.Problem(("title", $"Title must be at most {ValidationConstants.DecisionTitleMax} characters."));

            if (req.MoodBefore is not null && (req.MoodBefore is < ValidationConstants.MoodMin or > ValidationConstants.MoodMax))
                return ApiValidation.Problem(("moodBefore", $"MoodBefore must be between {ValidationConstants.MoodMin} and {ValidationConstants.MoodMax}."));

            if (req.Notes is not null && req.Notes.Length > ValidationConstants.DecisionNotesMax)
                return ApiValidation.Problem(("notes", $"Notes must be at most {ValidationConstants.DecisionNotesMax} characters."));


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
