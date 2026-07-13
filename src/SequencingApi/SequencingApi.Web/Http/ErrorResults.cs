using ErrorOr;

namespace SequencingApi.Web.Http;

/// <summary>Maps an <see cref="ErrorOr"/> failure list to an HTTP result at the API edge.</summary>
internal static class ErrorResults
{
    public static IResult Problem(List<Error> errors)
    {
        if (errors.Count == 0)
        {
            return Results.Problem();
        }

        if (errors.TrueForAll(error => error.Type == ErrorType.Validation))
        {
            var failures = errors
                .GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
            return Results.ValidationProblem(failures);
        }

        var first = errors[0];
        var statusCode = first.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(detail: first.Description, statusCode: statusCode);
    }
}
