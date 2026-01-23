using System.Security.Claims;
using DecisionApi.Database;
using DecisionApi.Dtos.CheckIns;
using DecisionApi.Extensions;
using Microsoft.EntityFrameworkCore;
using DecisionApi.Dtos;

namespace DecisionApi.Endpoints.CheckIns;

public static class CheckInEndpoints
{
    public static IEndpointRouteBuilder MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/check-ins")
            .RequireAuthorization();

        group.MapPost("/", AddCheckIn).RequireRateLimiting("writes");


        return app;
    }

    private static async Task<IResult> AddCheckIn(
        CreateCheckInRequest req,
        AppDbContext db,
        ClaimsPrincipal user)
    {
        var userId = user.GetUserId();

        // validation
        if (req.MoodAfter is < ValidationConstants.MoodMin or > ValidationConstants.MoodMax)
            return ApiValidation.Problem(("moodAfter", $"MoodAfter must be between {ValidationConstants.MoodMin} and {ValidationConstants.MoodMax}."));

        if (req.Note is not null && req.Note.Length > ValidationConstants.CheckInNoteMax)
            return ApiValidation.Problem(("note", $"Note must be at most {ValidationConstants.CheckInNoteMax} characters."));


        // owner-only: ensure the decision belongs to this user
        var decision = await db.Decisions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == req.DecisionId && d.UserId == userId);

        if (decision is null)
            return Results.NotFound(); // hides existence of others' decisions

        var checkIn = new Models.CheckIn
        {
            DecisionId = req.DecisionId,
            UserId = userId,
            MoodAfter = req.MoodAfter,
            Note = req.Note,
            CreatedAt = DateTime.UtcNow
        };

        db.CheckIns.Add(checkIn);
        await db.SaveChangesAsync();

        var created = new
        {
            checkIn.Id,
            checkIn.DecisionId,
            checkIn.MoodAfter,
            checkIn.Note,
            checkIn.CreatedAt
        };
        return Results.Created($"/decisions/{req.DecisionId}/check-ins", created);

    }
}
