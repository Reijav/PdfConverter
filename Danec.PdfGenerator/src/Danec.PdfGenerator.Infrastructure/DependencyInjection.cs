using Danec.PdfGenerator.Application.Documents.GeneratePdfWithBackground;
using Danec.PdfGenerator.Infrastructure.Backgrounds;
using Danec.PdfGenerator.Infrastructure.Engines;
using Danec.PdfGenerator.Infrastructure.Engines.Chromium;
using Danec.PdfGenerator.Infrastructure.Engines.Docx;
using Danec.PdfGenerator.Infrastructure.Engines.HtmlRenderer;
using Danec.PdfGenerator.Infrastructure.Engines.IText;
using Danec.PdfGenerator.Infrastructure.Engines.Overlay;
using Danec.PdfGenerator.Infrastructure.Engines.QuestPdf;
using Danec.PdfGenerator.Infrastructure.Fonts;
using Danec.PdfGenerator.Infrastructure.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Danec.PdfGenerator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PdfGeneratorOptions>()
            .Bind(configuration.GetSection(PdfGeneratorOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.Chromium.MaxConcurrency is >= 1 and <= 32, "Pdf:Chromium:MaxConcurrency debe estar entre 1 y 32.")
            .Validate(o => o.Chromium.TimeoutSeconds is >= 5 and <= 300, "Pdf:Chromium:TimeoutSeconds debe estar entre 5 y 300.")
            .Validate(o => Uri.TryCreate(o.Gotenberg.BaseUrl, UriKind.Absolute, out _), "Pdf:Gotenberg:BaseUrl debe ser una URL absoluta.")
            .Validate(o => o.MiniPdf.MaxConcurrency is >= 1 and <= 32, "Pdf:MiniPdf:MaxConcurrency debe estar entre 1 y 32.")
            .Validate(o => TemplateName.Create(o.Fondo.Default).IsSuccess, "Pdf:Fondo:Default debe ser un nombre sin extension (letras, numeros, '-' o '_').")
            .Validate(o => o.Fondo.MaxBytes is >= 1024 and <= 50 * 1024 * 1024, "Pdf:Fondo:MaxBytes debe estar entre 1 KB y 50 MB.")
            .ValidateOnStart();

        // Plantillas (singleton: sin estado por solicitud; Scriban cachea las plantillas parseadas)
        services.AddSingleton<TemplatePaths>();
        services.AddSingleton<IHtmlTemplateRenderer, ScribanHtmlTemplateRenderer>();
        services.AddSingleton<ITemplateCatalog, FileSystemTemplateCatalog>();
        services.AddSingleton<IQuestPdfTemplate, FacturaQuestTemplate>();

        // Imagenes de fondo (Templates/fondo) y estampado detras del contenido con PDFsharp
        services.AddSingleton<IBackgroundImageStore, FileSystemBackgroundImageStore>();
        services.AddSingleton<IPdfBackgroundStamper, PdfSharpBackgroundStamper>();

        // Word: MiniWord rellena la plantilla; los "enrichers" agregan valores calculados por plantilla
        services.AddSingleton<IWordTemplateEnricher, FacturaWordEnricher>();
        services.AddSingleton<IWordDocumentGenerator, MiniWordDocumentGenerator>();

        // Gotenberg: cliente HTTP con nombre (IHttpClientFactory) consumido por un singleton
        services.AddHttpClient(GotenbergClient.HttpClientName, (sp, client) =>
        {
            var gotenberg = sp.GetRequiredService<IOptions<PdfGeneratorOptions>>().Value.Gotenberg;
            client.BaseAddress = new Uri(gotenberg.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(gotenberg.TimeoutSeconds);
        });
        services.AddSingleton<GotenbergClient>();

        // Un solo Chromium por proceso para cada motor de navegador
        services.AddSingleton<PuppeteerBrowserProvider>();
        services.AddSingleton<PlaywrightBrowserProvider>();

        // Motores: la clave es el enum PdfEngine; IPdfEngineFactory.Get(engine) devuelve el adaptador
        services.AddKeyedSingleton<IPdfEngine, ITextPdfEngine>(PdfEngine.IText);
        services.AddKeyedSingleton<IPdfEngine, QuestPdfEngine>(PdfEngine.QuestPdf);
        services.AddKeyedSingleton<IPdfEngine, HtmlRendererPdfEngine>(PdfEngine.HtmlRenderer);
        services.AddKeyedSingleton<IPdfEngine, OverlayPdfEngine>(PdfEngine.Overlay);
        services.AddKeyedSingleton<IPdfEngine, PuppeteerPdfEngine>(PdfEngine.Puppeteer);
        services.AddKeyedSingleton<IPdfEngine, PlaywrightPdfEngine>(PdfEngine.Playwright);
        services.AddKeyedSingleton<IPdfEngine, DocxPdfEngine>(PdfEngine.Docx);
        services.AddKeyedSingleton<IPdfEngine, MiniPdfDocxEngine>(PdfEngine.MiniPdf);
        services.AddSingleton<IPdfEngineFactory, PdfEngineFactory>();

        services.AddHealthChecks().AddCheck<TemplatesHealthCheck>("templates", tags: ["ready"]);

        // No es DI: iTextSharp 5 (binario .NET Framework) usa codificaciones como windows-1252, que .NET 10 no trae registradas
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        // No es DI: configuracion estatica de PDFsharp que debe ocurrir antes de usar cualquier fuente
        PdfSharpFontSetup.Configure(configuration[$"{PdfGeneratorOptions.SectionName}:FontsPath"]);

        // No es DI: MiniPdf registra fuentes a nivel de proceso (en Linux, Arial/Times/Courier -> Liberation)
        var miniPdf = configuration.GetSection($"{PdfGeneratorOptions.SectionName}:MiniPdf").Get<MiniPdfOptions>() ?? new MiniPdfOptions();
        MiniPdfFontSetup.Configure(miniPdf, configuration[$"{PdfGeneratorOptions.SectionName}:FontsPath"]);
        return services;
    }
}
