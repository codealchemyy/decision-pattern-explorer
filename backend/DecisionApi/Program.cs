using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DecisionApi.Dtos.Auth;
using DecisionApi.Database;
using DecisionApi.Models;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://decision-pattern-ui-final-project-h9cde9cvcgd0dnbk.germanywestcentral-01.azurewebsites.net"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();




// --- Database (SQLite) ---
var sqlitePath =
    builder.Configuration["Sqlite:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data", "app.db");

// 3) Ensure the folder exists (important for /home/data on Azure Linux)
var sqliteDir = Path.GetDirectoryName(sqlitePath);
if (!string.IsNullOrWhiteSpace(sqliteDir))
{
    Directory.CreateDirectory(sqliteDir);
}

// 4) Build connection string
var connectionString = $"Data Source={sqlitePath}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString)
           .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
);


var app = builder.Build();

var seedEnabled = app.Environment.IsDevelopment()
                  && builder.Configuration.GetValue<bool>("SeedData", true);
if (seedEnabled)
{
    await SeedData.EnsureSeededAsync(app.Services);
}


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}


app.UseRouting();
app.UseCors("Frontend");


var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/health", () => Results.Ok(new {status = "ok"}))
   .WithName("Health");


app.MapGet("/ready", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(title: "Database not reachable", statusCode: 503);
})
.WithName("Ready");

app.MapPost("/auth/register", async (
    RegisterRequest req,
    AppDbContext db,
    IPasswordHasher<User> hasher) =>
{
    // Minimal validation (MVP)
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

    // Check email exists (fast check)
    var exists = await db.Users.AnyAsync(u => u.Email.ToLower() == email);
    if (exists)
        return Results.Problem(title: "Email already exists", statusCode: StatusCodes.Status409Conflict);

    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = displayName,
        CreatedAt = DateTime.UtcNow,
        PasswordHash = "" // temporary, we overwrite it immediately next line
    };

    user.PasswordHash = hasher.HashPassword(user, req.Password);
    

    db.Users.Add(user);

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
        // race condition protection (unique index wins)
        return Results.Problem(title: "Email already exists", statusCode: StatusCodes.Status409Conflict);
    }

    return Results.Created($"/users/{user.Id}", new { user.Id, user.Email, user.DisplayName });
})
.WithName("AuthRegister");




app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
