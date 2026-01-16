using Microsoft.EntityFrameworkCore;
using DecisionApi.Database;

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

app.MapGet("/version", () =>
{
    var sha = Environment.GetEnvironmentVariable("WEBSITE_COMMIT_ID")
              ?? Environment.GetEnvironmentVariable("GITHUB_SHA")
              ?? "unknown";

    var fromConfig = builder.Configuration["Sqlite:Path"];
    var fromEnv = Environment.GetEnvironmentVariable("Sqlite__Path");

    return Results.Ok(new
    {
        sha,
        sqlite_from_config = fromConfig,
        sqlite_from_env = fromEnv
    });
});


app.MapGet("/ready", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return canConnect
            ? Results.Ok(new { status = "ready" })
            : Results.Problem(title: "Database not reachable", statusCode: 503);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database exception",
            detail: ex.ToString(),   // TEMP for Azure debugging
            statusCode: 503
        );
    }
})
.WithName("Ready");


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
