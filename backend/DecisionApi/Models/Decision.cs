namespace DecisionApi.Models;

public enum DecisionVisibility
{
    Private = 0,
    Anonymous = 1,
    PublicNickname = 2
}

public class Decision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Relations (FKs)
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    // Core fields
    public required string Title { get; set; }
    public string? Notes { get; set; }

    // MVP mood snapshot on creation (simple int scale 1..5)
    public int MoodBefore { get; set; }

    public DecisionVisibility Visibility { get; set; } = DecisionVisibility.Private;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
