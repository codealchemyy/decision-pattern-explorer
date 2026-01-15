namespace DecisionApi.Models;

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Relations (FKs)
    public Guid CommunityPostId { get; set; }
    public CommunityPost? CommunityPost { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    // Core fields
    public required string Text { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
