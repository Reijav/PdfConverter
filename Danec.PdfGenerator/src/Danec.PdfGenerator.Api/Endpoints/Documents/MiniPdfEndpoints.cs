using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Danec.PdfGenerator.Application.Documents.GeneratePdf;
using Danec.PdfGenerator.Application.Documents.GenerateWord;
using Danec.PdfGenerator.Domain.Documents;
using Microsoft.Extensions.Validation;

namespace Danec.PdfGenerator.Api.Endpoints.Documents;

#pragma warning disable ASP0029 // [SkipValidation] es experimental; el validador de .NET 10 no soporta JsonElement

/// <summary>Solicitud para generar un documento desde una plantilla Word con MiniWord + MiniPdf.</summary>
/// <param name="Template">Plantilla Word: Templates/docx/{template}.docx.</param>
/// <param name="Data">Parametros. Objetos anidados se escriben como {{padre_hijo}}; arreglos como filas {{lista.campo}}.</param>
/// <param name="FileName">Nombre del archivo devuelto (sin ruta ni extension).</param>
public sealed record MiniPdfRequest(
    [StringLength(TemplateName.MaxLength)] string? Template,
    [SkipValidation] JsonElement? Data,
    [StringLength(120)] string? FileName);

#pragma warning restore ASP0029

/// <summary>
/// Plantilla Word sin servicios externos: MiniWord rellena el .docx y MiniPdf lo convierte a PDF dentro del proceso.
/// Alternativa a /pdf/docx (Gotenberg). MiniPdf 0.x no resuelve campos de pagina (Pagina X de Y) ni genera PDF/A.
/// </summary>
public static class MiniPdfEndpoints
{
    private const string PdfContentType = "application/pdf";

    public static IEndpointRouteBuilder MapMiniPdfEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/minipdf").WithTags("MiniPdf");

        group.MapPost("/docx", GenerateDocxAsync)
            .WithName("MiniPdfGenerateDocx")
            .WithSummary("MiniWord: plantilla Word + parametros -> .docx")
            .WithDescription("Rellena Templates/docx/{template}.docx con los parametros y devuelve el Word editable. Es el mismo .docx que convierte POST /api/v1/minipdf/pdf.")
            .Produces(StatusCodes.Status200OK, contentType: WordEndpoints.DocxContentType)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/pdf", GeneratePdfAsync)
            .WithName("MiniPdfGeneratePdf")
            .WithSummary("MiniWord + MiniPdf: plantilla Word + parametros -> PDF (sin Gotenberg)")
            .WithDescription("Rellena la plantilla Word con MiniWord y la convierte a PDF en proceso con MiniPdf (Apache 2.0). Tamano y margenes los define el .docx. Limitaciones: sin campos de numero de pagina, sin bordes de parrafo, sin PDF/A. Use ?inline=true para verlo en el navegador.")
            .Produces(StatusCodes.Status200OK, contentType: PdfContentType)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GenerateDocxAsync(
        MiniPdfRequest request,
        GenerateWordHandler handler,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GenerateWordCommand(request.Template, request.Data ?? default, request.FileName), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        http.Response.Headers["X-Render-Time-Ms"] = result.Value.ElapsedMs.ToString(CultureInfo.InvariantCulture);
        return TypedResults.File(result.Value.Content, WordEndpoints.DocxContentType, result.Value.FileName);
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GeneratePdfAsync(
        MiniPdfRequest request,
        bool? inline,
        GeneratePdfHandler handler,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var command = new GeneratePdfCommand(
            PdfEngine.MiniPdf,
            request.Template,
            Html: null,
            request.Data ?? default,
            PageSize: null,
            Orientation: null,
            MarginMm: null,
            request.FileName);

        var result = await handler.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        var pdf = result.Value;
        http.Response.Headers["X-Pdf-Engine"] = pdf.Engine.ToString();
        http.Response.Headers["X-Render-Time-Ms"] = pdf.ElapsedMs.ToString(CultureInfo.InvariantCulture);

        if (inline ?? false)
        {
            http.Response.Headers.ContentDisposition = $"inline; filename=\"{pdf.FileName}\"";
            return TypedResults.File(pdf.Content, PdfContentType);
        }

        return TypedResults.File(pdf.Content, PdfContentType, pdf.FileName);
    }
}
