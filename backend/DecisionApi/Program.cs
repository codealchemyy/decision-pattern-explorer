using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using DecisionApi.Database;
using DecisionApi.Models;
using DecisionApi.Endpoints.Auth;


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

app.MapAuthEndpoints();



app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
