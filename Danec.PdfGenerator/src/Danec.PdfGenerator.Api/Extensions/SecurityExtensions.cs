namespace Danec.PdfGenerator.Api.Extensions;

/// <summary>API sin autenticacion propia (p.ej. detras de un gateway que ya autentica en red interna).</summary>
public static class SecurityExtensions
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication();
        services.AddAuthorization();
        return services;
    }
}
