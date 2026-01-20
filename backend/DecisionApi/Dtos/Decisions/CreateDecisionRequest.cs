using DecisionApi.Models;

namespace DecisionApi.Dtos.Decisions;

public sealed class CreateDecisionRequest
{
    public required Guid CategoryId { get; init; }
    public required string Title { get; init; }
    public string? Notes { get; init; }
    public int MoodBefore { get; init; } // you can validate 1..5
    public DecisionVisibility Visibility { get; init; } = DecisionVisibility.Private;
}
