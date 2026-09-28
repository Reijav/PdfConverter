using Microsoft.AspNetCore.Diagnostics;

namespace Danec.PdfGenerator.Api.Infrastructure;

internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            BadHttpRequestException bad => (bad.StatusCode, "Solicitud invalida."),
            OperationCanceledException => (499, "Solicitud cancelada por el cliente."),
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Title = title, Status = status },
        }).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Excepcion no controlada")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
