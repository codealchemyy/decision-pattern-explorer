namespace DecisionApi.Models;

public class CheckIn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Relations (FKs)
    public Guid DecisionId { get; set; }
    public Decision? Decision { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    // Core fields
    public int MoodAfter { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
