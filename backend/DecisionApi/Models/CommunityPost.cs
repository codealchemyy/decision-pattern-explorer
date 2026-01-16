namespace DecisionApi.Models;

public class CommunityPost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Relations
    public Guid DecisionId { get; set; }
    public Decision? Decision { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    // Snapshot fields (so feed works even if Decision changes later)
    public string Title { get; set; } = string.Empty;

    public DecisionVisibility Visibility { get; set; } = DecisionVisibility.Anonymous;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
