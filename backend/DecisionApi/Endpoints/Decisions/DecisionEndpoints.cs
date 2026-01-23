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
        group.MapPost("/", CreateDecision).RequireRateLimiting("writes");
        group.MapPost("/{id:guid}/check-ins", AddCheckIn).RequireRateLimiting("writes");
        group.MapGet("/{id:guid}", GetById);
        group.MapGet("/{id:guid}/check-ins", GetCheckInsForDecision);
        group.MapPatch("/{id:guid}", UpdateDecision).RequireRateLimiting("writes");
        group.MapDelete("/{id:guid}", DeleteDecision).RequireRateLimiting("writes");
        group.MapPatch("/{id:guid}/visibility", UpdateVisibility).RequireRateLimiting("writes");

        

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
            //if (req.Visibility is not null) decision.Visibility = req.Visibility.Value;

            // allow clearing notes via empty string
            if (req.Notes is not null)
                decision.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes;

            await db.SaveChangesAsync();

            // keep community post snapshot in sync (only if a post exists)
            var post = await db.CommunityPosts.FirstOrDefaultAsync(p => p.DecisionId == decision.Id);

            if (post is not null)
            {
                // If the decision is private, remove the post (extra safety)
                if (decision.Visibility == Models.DecisionVisibility.Private)
                {
                    db.CommunityPosts.Remove(post);
                }
                else
                {
                    // Otherwise just sync snapshot fields
                    post.Title = decision.Title;
                    post.CategoryId = decision.CategoryId;
                    post.Visibility = decision.Visibility;

                    // AuthorDisplayName is owned by UpdateVisibility (single source of truth)
                    // so we DON'T set it here.
                }

                await db.SaveChangesAsync();
            }


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


        private static async Task<IResult> UpdateVisibility(
            Guid id,
            UpdateDecisionVisibilityRequest req,
            AppDbContext db,
            ClaimsPrincipal user)
        {
            var userId = user.GetUserId();
            
            if (!Enum.IsDefined(typeof(Models.DecisionVisibility), req.Visibility))
            return ApiValidation.Problem(("visibility", "Invalid visibility value."));

            var decision = await db.Decisions
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
            

            if (decision is null)
                return Results.NotFound();

            decision.Visibility = req.Visibility;
            await db.SaveChangesAsync();

            // If Private -> remove post (so it cannot appear in feed)
            if (decision.Visibility == Models.DecisionVisibility.Private)
            {
                var existing = await db.CommunityPosts
                    .FirstOrDefaultAsync(p => p.DecisionId == decision.Id);

                if (existing is not null)
                {
                    db.CommunityPosts.Remove(existing);
                    await db.SaveChangesAsync();
                }

                return Results.NoContent();
            }

            // Anonymous/PublicNickname -> upsert post (1 decision = 1 post)
            var post = await db.CommunityPosts
                .FirstOrDefaultAsync(p => p.DecisionId == decision.Id);

            if (post is null)
            {
                post = new Models.CommunityPost
                {
                    DecisionId = decision.Id,
                    CategoryId = decision.CategoryId,
                    Title = decision.Title,
                    Visibility = decision.Visibility,
                    CreatedAt = DateTime.UtcNow,
                    AuthorDisplayName = decision.Visibility == Models.DecisionVisibility.PublicNickname
                        ? decision.User?.DisplayName
                        : null
                };

                db.CommunityPosts.Add(post);
            }
            else
            {
                post.CategoryId = decision.CategoryId;
                post.Title = decision.Title;
                post.Visibility = decision.Visibility;
                post.AuthorDisplayName = decision.Visibility == Models.DecisionVisibility.PublicNickname
                    ? decision.User?.DisplayName
                    : null;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                decision.Id,
                decision.Visibility
            });
        }


        private static async Task<IResult> AddCheckIn(
            Guid id,
            CreateCheckInRequest req,
            AppDbContext db,
            ClaimsPrincipal user)
        {
            var userId = user.GetUserId();

            // owner-only (hide existence)
            var ownsDecision = await db.Decisions
                .AsNoTracking()
                .AnyAsync(d => d.Id == id && d.UserId == userId);

            if (!ownsDecision)
                return Results.NotFound();

            if (req.MoodAfter is < ValidationConstants.MoodMin or > ValidationConstants.MoodMax)
                return ApiValidation.Problem(("moodAfter", $"MoodAfter must be between {ValidationConstants.MoodMin} and {ValidationConstants.MoodMax}."));

            if (req.Note is not null && req.Note.Length > ValidationConstants.CheckInNoteMax)
                return ApiValidation.Problem(("note", $"Note must be at most {ValidationConstants.CheckInNoteMax} characters."));

            var checkIn = new Models.CheckIn
            {
                DecisionId = id,
                UserId = userId,
                MoodAfter = req.MoodAfter,
                Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            db.CheckIns.Add(checkIn);
            await db.SaveChangesAsync();

            return Results.Created($"/decisions/{id}/check-ins/{checkIn.Id}", new
            {
                checkIn.Id,
                checkIn.MoodAfter,
                Note = checkIn.Note,
                checkIn.CreatedAt
            });
        }





}
