using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Danec.PdfGenerator.Application.Documents.GeneratePdf;
using Danec.PdfGenerator.Application.Documents.GeneratePdfWithBackground;
using Danec.PdfGenerator.Domain.Documents;
using Microsoft.Extensions.Validation;

namespace Danec.PdfGenerator.Api.Endpoints.Documents;

#pragma warning disable ASP0029 // [SkipValidation] es experimental; el validador de .NET 10 no soporta JsonElement

/// <summary>Igual que <see cref="GeneratePdfRequest"/> mas la imagen de fondo.</summary>
/// <param name="Template">Plantilla HTML registrada, p.ej. "factura".</param>
/// <param name="Html">Plantilla Scriban en linea (alternativa a template).</param>
/// <param name="Data">Parametros (JSON libre).</param>
/// <param name="Page">Tamano, orientacion y margen.</param>
/// <param name="FileName">Nombre del archivo devuelto (sin ruta).</param>
/// <param name="Fondo">Imagen de fondo. Si se omite se usa Pdf:Fondo:Default en todas las paginas, ajustada al ancho.</param>
public sealed record GeneratePdfFondoRequest(
    [StringLength(TemplateName.MaxLength)] string? Template,
    [StringLength(TemplateSource.MaxInlineLength)] string? Html,
    [SkipValidation] JsonElement? Data,
    PageRequest? Page,
    [StringLength(120)] string? FileName,
    FondoRequest? Fondo);

#pragma warning restore ASP0029

/// <summary>Imagen de fondo a estampar detras del contenido.</summary>
/// <param name="Imagen">Nombre en Templates/fondo sin extension (.png, .jpg o .jpeg), p.ej. "fondo-de-cartas".</param>
/// <param name="Paginas">Todas (defecto) o Primera.</param>
/// <param name="Ajuste">Ancho (defecto): todo el ancho, alto proporcional. Pagina: estirada a la pagina. Centrado: tamano natural centrada.</param>
public sealed record FondoRequest(
    [StringLength(TemplateName.MaxLength)] string? Imagen,
    BackgroundPages? Paginas,
    BackgroundFit? Ajuste);

/// <summary>
/// PDF con HtmlRenderer + imagen de fondo estampada despues con PDFsharp (detras del contenido).
/// Se mapea desde Program.cs con <c>api.MapPdfFondoEndpoints()</c>.
/// </summary>
public static class PdfFondoEndpoints
{
    private const string PdfContentType = "application/pdf";

    public static IEndpointRouteBuilder MapPdfFondoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/pdf").WithTags("PDF")
            .MapPost("/htmlrenderer-fondo", GenerateAsync)
            .WithName("GeneratePdfHtmlRendererFondo")
            .WithSummary("Scriban + HtmlRenderer.PdfSharp + imagen de fondo")
            .WithDescription(
                "Genera el PDF igual que POST /api/v1/pdf/htmlrenderer y despues estampa una imagen de Templates/fondo " +
                "DETRAS del contenido (texto, bordes y rellenos quedan visibles). 'fondo' es opcional: imagen " +
                "(defecto Pdf:Fondo:Default), paginas (Todas|Primera) y ajuste (Ancho|Pagina|Centrado). " +
                "La plantilla debe tener fondo transparente (sin background en body). Use ?inline=true para verlo en el navegador.")
            .Produces(StatusCodes.Status200OK, contentType: PdfContentType)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GenerateAsync(
        GeneratePdfFondoRequest request,
        bool? inline,
        GeneratePdfWithBackgroundHandler handler,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var command = new GeneratePdfWithBackgroundCommand(
            new GeneratePdfCommand(
                PdfEngine.HtmlRenderer,
                request.Template,
                request.Html,
                request.Data ?? default,
                request.Page?.Size,
                request.Page?.Orientation,
                request.Page?.MarginMm,
                request.FileName),
            request.Fondo?.Imagen,
            request.Fondo?.Paginas,
            request.Fondo?.Ajuste);

        var result = await handler.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        var pdf = result.Value.Pdf;
        http.Response.Headers["X-Pdf-Engine"] = pdf.Engine.ToString();
        http.Response.Headers["X-Render-Time-Ms"] = pdf.ElapsedMs.ToString(CultureInfo.InvariantCulture);
        http.Response.Headers["X-Background"] = result.Value.Background;
        http.Response.Headers["X-Background-Ms"] = result.Value.BackgroundMs.ToString(CultureInfo.InvariantCulture);

        if (inline ?? false)
        {
            http.Response.Headers.ContentDisposition = $"inline; filename=\"{pdf.FileName}\"";
            return TypedResults.File(pdf.Content, PdfContentType);
        }

        return TypedResults.File(pdf.Content, PdfContentType, pdf.FileName);
    }
}
