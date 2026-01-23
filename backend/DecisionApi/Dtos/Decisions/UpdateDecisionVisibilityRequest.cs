using DecisionApi.Models;

namespace DecisionApi.Dtos.Decisions;

public sealed class UpdateDecisionVisibilityRequest
{
    public required DecisionVisibility Visibility { get; set; }
}
