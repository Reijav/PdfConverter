using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Danec.PdfGenerator.Api.Infrastructure;

namespace Danec.PdfGenerator.Api.Extensions;

public static class PresentationExtensions
{
    public const string CorsPolicy = "default";
    public const string RateLimitPolicy = "fixed";

    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidation();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
            ctx.ProblemDetails.Extensions.TryAdd("requestId", ctx.HttpContext.TraceIdentifier);
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddOpenApi();

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(
            CorsPolicy,
            policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()
                .WithExposedHeaders("Content-Disposition", "X-Pdf-Engine", "X-Render-Time-Ms", "X-Background", "X-Background-Ms")));

        var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 100);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) }));
        });

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return services;
    }
}
