namespace Danec.PdfGenerator.Api.Extensions;

public static class ResultExtensions
{
    public static ProblemHttpResult ToProblem(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Un Result exitoso no puede convertirse en ProblemDetails.");
        }

        return result.Error.ToProblem();
    }

    public static ProblemHttpResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var (status, type) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "https://tools.ietf.org/html/rfc9110#section-15.5.2"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "https://tools.ietf.org/html/rfc9110#section-15.5.4"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            _ => (StatusCodes.Status500InternalServerError, "https://tools.ietf.org/html/rfc9110#section-15.6.1"),
        };

        return TypedResults.Problem(
            title: error.Code,
            detail: error.Description,
            statusCode: status,
            type: type,
            extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });
    }
}
