namespace DecisionApi.Dtos.Auth;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);
