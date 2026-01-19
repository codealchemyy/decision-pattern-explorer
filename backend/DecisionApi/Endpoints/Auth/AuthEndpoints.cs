using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DecisionApi.Database;
using DecisionApi.Dtos.Auth;
using DecisionApi.Models;

namespace DecisionApi.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);

        return app;
    }
    
    private static async Task<IResult> Register(
        RegisterRequest req,
        AppDbContext db,
        IPasswordHasher<User> hasher)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(req.Email))
            errors["email"] = new[] { "Email is required." };

        if (string.IsNullOrWhiteSpace(req.DisplayName))
            errors["displayName"] = new[] { "DisplayName is required." };

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
            errors["password"] = new[] { "Password must be at least 8 characters." };

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var email = req.Email.Trim().ToLowerInvariant();
        var displayName = req.DisplayName.Trim();

        var exists = await db.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (exists)
            return Results.Problem(title: "Email already exists", statusCode: StatusCodes.Status409Conflict);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = ""
        };

        user.PasswordHash = hasher.HashPassword(user, req.Password);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Results.Problem(title: "Email already exists", statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/users/{user.Id}", new { user.Id, user.Email, user.DisplayName });
    }

    private static async Task<IResult> Login(
        LoginRequest req,
        AppDbContext db,
        IPasswordHasher<User> hasher,
        IConfiguration config)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(req.Email))
            errors["email"] = new[] { "Email is required." };

        if (string.IsNullOrWhiteSpace(req.Password))
            errors["password"] = new[] { "Password is required." };

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var email = req.Email.Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user is null)
            return Results.Problem(title: "Invalid credentials", statusCode: StatusCodes.Status401Unauthorized);

        var verified = hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (verified == PasswordVerificationResult.Failed)
            return Results.Problem(title: "Invalid credentials", statusCode: StatusCodes.Status401Unauthorized);

        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var issuer = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];
        var expiresMinutes = config.GetValue("Jwt:ExpiresMinutes", 60);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("displayName", user.DisplayName)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(issuer) ? null : issuer,
            audience: string.IsNullOrWhiteSpace(audience) ? null : audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        return Results.Ok(new { token = jwt });
    }
}
