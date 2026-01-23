namespace DecisionApi.Dtos.Decisions;

public sealed class CreateCheckInRequest
{
    public int MoodAfter { get; set; }
    public string? Note { get; set; }
}
