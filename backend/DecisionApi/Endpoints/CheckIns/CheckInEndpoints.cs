using System.Security.Claims;
using DecisionApi.Database;
using DecisionApi.Dtos.CheckIns;
using DecisionApi.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DecisionApi.Endpoints.CheckIns;

public static class CheckInEndpoints
{
    public static IEndpointRouteBuilder MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/check-ins")
            .RequireAuthorization();

        group.MapPost("/", AddCheckIn);

        return app;
    }

    private static async Task<IResult> AddCheckIn(
        CreateCheckInRequest req,
        AppDbContext db,
        ClaimsPrincipal user)
    {
        var userId = user.GetUserId();

        // validation
        if (req.MoodAfter is < 1 or > 5)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["moodAfter"] = new[] { "MoodAfter must be between 1 and 5." }
            });

        if (req.Note is not null && req.Note.Length > 500)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["note"] = new[] { "Note must be at most 500 characters." }
            });

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
