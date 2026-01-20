using DecisionApi.Database;
using Microsoft.EntityFrameworkCore;

namespace DecisionApi.Endpoints.Categories;

public static class CategoriesEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var cats = await db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            return Results.Ok(cats);
        });

        return app;
    }
}
