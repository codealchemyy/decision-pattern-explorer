using DecisionApi.Models;

namespace DecisionApi.Dtos.Decisions;

public sealed class UpdateDecisionRequest
{
    public string? Title { get; init; }
    public string? Notes { get; init; }
    public int? MoodBefore { get; init; } // optional update, validate 1..5
    public DecisionVisibility? Visibility { get; init; } // optional update
    public Guid? CategoryId { get; init; } // optional update
}
