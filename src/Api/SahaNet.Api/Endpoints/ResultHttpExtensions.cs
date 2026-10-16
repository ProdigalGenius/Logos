using SahaNet.Domain.Common;

namespace SahaNet.Api.Endpoints;

internal static class ResultHttpExtensions
{
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(statusCode: status, title: error.Code, detail: error.Message);
    }

    public static IResult ToHttp<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();

    public static IResult ToHttp(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
}
