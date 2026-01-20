namespace DecisionApi.Dtos;

public static class ApiValidation
{
    public static IResult Problem(params (string Field, string Message)[] errors)
    {
        var dict = new Dictionary<string, string[]>();

        foreach (var (field, message) in errors)
        {
            if (!dict.TryGetValue(field, out var existing))
                dict[field] = new[] { message };
            else
                dict[field] = existing.Concat(new[] { message }).ToArray();
        }

        return Results.ValidationProblem(
            dict,
            title: "One or more validation errors occurred.",
            statusCode: StatusCodes.Status400BadRequest
        );
    }
}
