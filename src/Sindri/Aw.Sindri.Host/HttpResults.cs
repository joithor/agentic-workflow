using Aw.Kernel;

namespace Aw.Sindri.Host;

/// <summary>Maps a returned Result to TypedResults (a value) or ProblemDetails (an error, by its kind).</summary>
internal static class HttpResults
{
    internal static IResult ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : Problem(result.Error);

    /// <summary>An error as a ProblemDetails answer: title the code, detail the message, the status its kind's; extension members (RFC 9457 §3.2) follow when it has any.</summary>
    internal static IResult Problem(Error error)
        => error.Extensions is { Count: > 0 }
            ? new ProblemWithExtensions(error, StatusOf(error.Kind))
            : TypedResults.Problem(title: error.Code, detail: error.Message, statusCode: StatusOf(error.Kind));

    private static int StatusOf(ErrorKind kind) => kind switch
    {
        ErrorKind.Invalid => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.RateLimited or ErrorKind.BudgetExceeded => StatusCodes.Status429TooManyRequests,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
