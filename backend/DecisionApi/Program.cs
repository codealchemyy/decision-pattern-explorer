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
var dbFolder = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dbFolder);

var dbPath = Path.Combine(dbFolder, "app.db");
var connectionString = $"Data Source={dbPath}";

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


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
