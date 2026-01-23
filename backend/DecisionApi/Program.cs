using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using DecisionApi.Database;
using DecisionApi.Models;
using DecisionApi.Endpoints.Auth;
using DecisionApi.Extensions;
using DecisionApi.Endpoints.Decisions;
using DecisionApi.Endpoints.Categories;
using DecisionApi.Endpoints.CheckIns;
using DecisionApi.Dtos.Decisions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using DecisionApi.Endpoints.Community;





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

builder.Services.AddJwtAuth(builder.Configuration);

builder.Services.AddProblemDetails();


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("writes", context =>
    {
        // Prefer per-user limiting when authenticated, else per IP
        var userId =
            context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User?.FindFirst("sub")?.Value;

        var key = !string.IsNullOrWhiteSpace(userId)
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.OnRejected = async (rejectionContext, ct) =>
    {
        var http = rejectionContext.HttpContext;

        TimeSpan? retryAfter = null;
        if (rejectionContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra))
            retryAfter = ra;

        if (retryAfter is not null)
            http.Response.Headers.RetryAfter = ((int)retryAfter.Value.TotalSeconds).ToString();

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.8",
            Detail = retryAfter is null
                ? "Rate limit exceeded. Please try again soon."
                : $"Rate limit exceeded. Try again in {(int)retryAfter.Value.TotalSeconds} seconds."
        };

        await Results.Json(
            problem,
            statusCode: StatusCodes.Status429TooManyRequests,
            contentType: "application/problem+json"
        ).ExecuteAsync(http);
    };


    /* options.OnRejected = async (rejectionContext, ct) =>
    {
        var http = rejectionContext.HttpContext;

        // Try to get retry-after info from the limiter
        TimeSpan? retryAfter = null;
        if (rejectionContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra))
            retryAfter = ra;

        if (retryAfter is not null)
            http.Response.Headers.RetryAfter = ((int)retryAfter.Value.TotalSeconds).ToString();

        //http.Response.ContentType = "application/problem+json";

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.8",
            Detail = retryAfter is null
                ? "Rate limit exceeded. Please try again soon."
                : $"Rate limit exceeded. Try again in {(int)retryAfter.Value.TotalSeconds} seconds."
        };

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        await http.Response.WriteAsJsonAsync(
            problem,
            contentType: "application/problem+json",
            cancellationToken: ct
        );
    }; */
});



var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}


var seedEnabled = app.Environment.IsDevelopment()
                  && builder.Configuration.GetValue<bool>("SeedData", true);
if (seedEnabled)
{
    await SeedData.EnsureSeededAsync(app.Services);
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;

        var result = Results.Problem(
            title: "An unexpected error occurred.",
            detail: app.Environment.IsDevelopment() ? ex?.Message : null,
            statusCode: StatusCodes.Status500InternalServerError,
            type: "https://tools.ietf.org/html/rfc9110#section-15.6.1"
        );

        await result.ExecuteAsync(context);
    });
});


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
app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();



var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};


app.MapGet("/health", () => Results.Ok(new {status = "ok"}))
   .WithName("Health").DisableRateLimiting();

app.MapGet("/ready", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(title: "Database not reachable", statusCode: 503);
})
.WithName("Ready").DisableRateLimiting();



app.MapAuthEndpoints();
app.MapDecisionEndpoints();
app.MapCategoryEndpoints();
app.MapCheckInEndpoints();
app.MapCommunityEndpoints();





app.Run();