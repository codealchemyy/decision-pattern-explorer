namespace DecisionApi.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Email { get; set; }
    public required string DisplayName { get; set; }

    // For FR005 later (Auth). For now we just store it.
    public required string PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
