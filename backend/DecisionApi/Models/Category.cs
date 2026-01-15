namespace DecisionApi.Models;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
}
