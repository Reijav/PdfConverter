using Danec.PdfGenerator.Application.Documents.GeneratePdf;
using Danec.PdfGenerator.Application.Documents.GeneratePdfWithBackground;
using Danec.PdfGenerator.Application.Documents.GenerateWord;
using Danec.PdfGenerator.Application.Documents.PreviewHtml;
using Danec.PdfGenerator.Application.Documents.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace Danec.PdfGenerator.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Casos de uso. Registro explicito: un caso de uso nuevo = una linea aqui.
    /// Scoped porque se resuelven por solicitud HTTP; sus dependencias (motores, catalogo) son singleton.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GeneratePdfHandler>();
        services.AddScoped<GeneratePdfWithBackgroundHandler>();
        services.AddScoped<GenerateWordPdfWithBackgroundHandler>();
        services.AddScoped<GenerateWordHandler>();
        services.AddScoped<PreviewHtmlHandler>();
        services.AddScoped<ListTemplatesHandler>();
        services.AddScoped<GetTemplateSampleHandler>();

        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
