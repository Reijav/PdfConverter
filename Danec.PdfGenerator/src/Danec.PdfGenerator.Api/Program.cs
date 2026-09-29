using Danec.PdfGenerator.Application;
using Danec.PdfGenerator.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddObservability();
    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddPresentation(builder.Configuration)
        .AddSecurity(builder.Configuration);

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();
    }

    app.UseCors(PresentationExtensions.CorsPolicy);
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    var api = app.MapGroup("/api/v1").RequireRateLimiting(PresentationExtensions.RateLimitPolicy);
    if (app.Configuration.GetValue<bool>("Security:Enabled"))
    {
        api.RequireAuthorization();
    }

    api.MapPdfEndpoints();
    api.MapWordEndpoints();
    api.MapMiniPdfEndpoints();

    app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "La aplicacion termino inesperadamente");
}
finally
{
    await Log.CloseAndFlushAsync();
}
