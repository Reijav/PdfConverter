using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

[assembly: AssemblyFixture(typeof(Danec.PdfGenerator.Api.IntegrationTests.ApiFactory))]

namespace Danec.PdfGenerator.Api.IntegrationTests;

/// <summary>
/// Una sola instancia de la API para todo el ensamblado: Chromium se inicia una vez y el logger
/// estatico de Serilog no se congela dos veces.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// El host se construye aqui, una vez, antes de cualquier test. Si se construye de forma perezosa,
    /// dos clases de test en paralelo lo crean a la vez y una falla con
    /// "The entry point exited without ever building an IHost".
    /// </summary>
    public ValueTask InitializeAsync()
    {
        _ = Services;
        return ValueTask.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimiting:PermitLimit", "10000");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }
}
