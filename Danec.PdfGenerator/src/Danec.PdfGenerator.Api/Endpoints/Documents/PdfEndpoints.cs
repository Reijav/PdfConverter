using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Danec.PdfGenerator.Application.Documents;
using Danec.PdfGenerator.Application.Documents.GeneratePdf;
using Danec.PdfGenerator.Application.Documents.PreviewHtml;
using Danec.PdfGenerator.Application.Documents.Templates;
using Danec.PdfGenerator.Domain.Documents;
using Microsoft.Extensions.Validation;

namespace Danec.PdfGenerator.Api.Endpoints.Documents;

// El validador de Minimal APIs (.NET 10) recorre JsonElement y falla en su indexador;
// [SkipValidation] (experimental, ASP0029) excluye los parametros libres de la validacion.
#pragma warning disable ASP0029

/// <summary>Solicitud de generacion. Envie <c>template</c> (plantilla registrada) o <c>html</c> (plantilla Scriban en linea).</summary>
/// <param name="Template">Nombre de la plantilla, p.ej. "factura". Ver GET /api/v1/pdf/templates.</param>
/// <param name="Html">HTML con sintaxis Scriban ({{ variable }}). Solo motores HTML.</param>
/// <param name="Data">Parametros que se inyectan en la plantilla (JSON libre, no se valida: el validador de .NET 10 no soporta JsonElement).</param>
/// <param name="Page">Tamano, orientacion y margen.</param>
/// <param name="FileName">Nombre del archivo devuelto (sin ruta).</param>
public sealed record GeneratePdfRequest(
    [StringLength(TemplateName.MaxLength)] string? Template,
    [StringLength(TemplateSource.MaxInlineLength)] string? Html,
    [SkipValidation] JsonElement? Data,
    PageRequest? Page,
    [StringLength(120)] string? FileName);

public sealed record PageRequest(
    PageSize? Size,
    PageOrientation? Orientation,
    [Range(0, PageSettings.MaxMarginMm)] double? MarginMm);

/// <summary>Solo para depurar plantillas HTML: devuelve el HTML combinado con los parametros.</summary>
/// <param name="Template">Plantilla registrada.</param>
/// <param name="Html">Plantilla Scriban en linea.</param>
/// <param name="Data">Parametros.</param>
/// <param name="Engine">Motor a simular (define pdf.motor y pdf.css3 en la plantilla). Opcional.</param>
public sealed record PreviewHtmlRequest(
    [StringLength(TemplateName.MaxLength)] string? Template,
    [StringLength(TemplateSource.MaxInlineLength)] string? Html,
    [SkipValidation] JsonElement? Data,
    PdfEngine? Engine = null);

#pragma warning restore ASP0029

/// <summary>
/// Endpoints de PDF. Se mapean explicitamente desde Program.cs con <c>api.MapPdfEndpoints()</c>.
/// Cada endpoint recibe el handler concreto de su caso de uso (registrado en Application.AddApplication).
/// </summary>
public static class PdfEndpoints
{
    private const string PdfContentType = "application/pdf";

    private static readonly (string Route, PdfEngine Engine, string Summary, string Description)[] Engines =
    [
        ("itext", PdfEngine.IText, "iTextSharp 5.5.13.1 + XMLWorker (AGPL)",
            "XHTML con CSS 2.1 (estilos y clases). Licencia AGPL. Plantillas: html."),
        ("questpdf", PdfEngine.QuestPdf, "QuestPDF 2022.12 (MIT)",
            "Documento definido en C# (clase IQuestPdfTemplate); los datos llegan como parametros. Plantillas: code."),
        ("htmlrenderer", PdfEngine.HtmlRenderer, "Scriban + HtmlRenderer.PdfSharp",
            "HTML 4 / CSS 2, 100 % .NET sin navegador. Plantillas: html."),
        ("overlay", PdfEngine.Overlay, "PDF base + texto superpuesto",
            "Escribe los parametros en coordenadas sobre un PDF disenado. Plantillas: overlay."),
        ("puppeteer", PdfEngine.Puppeteer, "PuppeteerSharp (Chromium)",
            "HTML5 / CSS3 completo, pie con numeracion de paginas. Plantillas: html."),
        ("playwright", PdfEngine.Playwright, "Microsoft.Playwright (Chromium)",
            "HTML5 / CSS3 completo, pie con numeracion de paginas. Plantillas: html."),
        ("docx", PdfEngine.Docx, "MiniWord + Gotenberg (plantilla Word)",
            "Rellena Templates/docx/{template}.docx con MiniWord y lo convierte con Gotenberg (LibreOffice). Tamano y margenes los define el .docx; solo aplica la orientacion. Requiere Gotenberg. Plantillas: word."),
    ];

    public static IEndpointRouteBuilder MapPdfEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/pdf").WithTags("PDF");

        // Un POST por motor; todos usan el mismo caso de uso con el motor fijo
        foreach (var (route, engine, summary, description) in Engines)
        {
            group.MapPost($"/{route}", (
                    GeneratePdfRequest request,
                    bool? inline,
                    GeneratePdfHandler handler,
                    HttpContext http,
                    CancellationToken cancellationToken) =>
                    GenerateAsync(engine, request, inline ?? false, handler, http, cancellationToken))
                .WithName($"GeneratePdf{engine}")
                .WithSummary(summary)
                .WithDescription(description + " Use ?inline=true para verlo en el navegador.")
                .Produces(StatusCodes.Status200OK, contentType: PdfContentType)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError);
        }

        group.MapPost("/preview-html", PreviewHtmlAsync)
            .WithName("PreviewHtml")
            .WithSummary("HTML combinado con los parametros (depuracion de plantillas)")
            .Produces(StatusCodes.Status200OK, contentType: "text/html");

        group.MapGet("/templates", ListTemplates)
            .WithName("ListTemplates")
            .WithSummary("Plantillas disponibles y los motores que las aceptan");

        group.MapGet("/templates/{name}/sample", GetSampleAsync)
            .WithName("GetTemplateSample")
            .WithSummary("Datos de ejemplo para probar una plantilla");

        return app;
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GenerateAsync(
        PdfEngine engine,
        GeneratePdfRequest request,
        bool inline,
        GeneratePdfHandler handler,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var command = new GeneratePdfCommand(
            engine,
            request.Template,
            request.Html,
            request.Data ?? default,
            request.Page?.Size,
            request.Page?.Orientation,
            request.Page?.MarginMm,
            request.FileName);

        var result = await handler.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        var pdf = result.Value;
        http.Response.Headers["X-Pdf-Engine"] = pdf.Engine.ToString();
        http.Response.Headers["X-Render-Time-Ms"] = pdf.ElapsedMs.ToString(CultureInfo.InvariantCulture);

        if (inline)
        {
            http.Response.Headers.ContentDisposition = $"inline; filename=\"{pdf.FileName}\"";
            return TypedResults.File(pdf.Content, PdfContentType);
        }

        return TypedResults.File(pdf.Content, PdfContentType, pdf.FileName);
    }

    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> PreviewHtmlAsync(
        PreviewHtmlRequest request,
        PreviewHtmlHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new PreviewHtmlCommand(request.Template, request.Html, request.Data ?? default, request.Engine), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Content(result.Value, "text/html; charset=utf-8")
            : result.ToProblem();
    }

    private static Ok<IReadOnlyList<TemplateDescriptor>> ListTemplates(ListTemplatesHandler handler) =>
        TypedResults.Ok(handler.Handle());

    private static async Task<Results<Ok<JsonElement>, ProblemHttpResult>> GetSampleAsync(
        string name,
        GetTemplateSampleHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(name, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }
}
