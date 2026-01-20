namespace DecisionApi.Dtos.CheckIns;

public sealed class CreateCheckInRequest
{
    public required Guid DecisionId { get; init; }
    public required int MoodAfter { get; init; } // validate 1..5
    public string? Note { get; init; }          // validate length
}
