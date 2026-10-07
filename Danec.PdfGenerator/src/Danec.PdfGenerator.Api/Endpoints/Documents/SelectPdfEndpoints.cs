using System.Globalization;
using Danec.PdfGenerator.Application.Documents.GeneratePdf;
using Danec.PdfGenerator.Domain.Documents;

namespace Danec.PdfGenerator.Api.Endpoints.Documents;

/// <summary>
/// SelectPdf.Universal (licencia comercial) como conversor a PDF, sin imagen de fondo:
/// HTML -> PDF con el Chromium del paquete y Word -> PDF en proceso. Sin clave de licencia
/// (Pdf:SelectPdf:LicenseKey) los PDF salen con la marca de agua de prueba. No convierte Excel.
/// </summary>
public static class SelectPdfEndpoints
{
    private const string PdfContentType = "application/pdf";

    public static IEndpointRouteBuilder MapSelectPdfEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/selectpdf").WithTags("SelectPdf");

        group.MapPost("/html-pdf", GenerateHtmlPdfAsync)
            .WithName("SelectPdfGenerateHtmlPdf")
            .WithSummary("Scriban + SelectPdf: plantilla HTML + parametros -> PDF")
            .WithDescription("Combina Templates/html/{template}.html (o el campo html) con los parametros y lo convierte con SelectPdf (Chromium: HTML5 / CSS3). Aplica tamano, orientacion y margen de 'page'. No agrega imagen de fondo. Use ?inline=true para verlo en el navegador.")
            .Produces(StatusCodes.Status200OK, contentType: PdfContentType)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/word-pdf", GenerateWordPdfAsync)
            .WithName("SelectPdfGenerateWordPdf")
            .WithSummary("MiniWord + SelectPdf: plantilla Word + parametros -> PDF")
            .WithDescription("Rellena Templates/docx/{template}.docx con MiniWord y lo convierte a PDF en proceso con SelectPdf. Tamano y margenes los define el .docx. No agrega imagen de fondo. Use ?inline=true para verlo en el navegador.")
            .Produces(StatusCodes.Status200OK, contentType: PdfContentType)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static Task<Results<FileContentHttpResult, ProblemHttpResult>> GenerateHtmlPdfAsync(
        GeneratePdfRequest request,
        bool? inline,
        GeneratePdfHandler handler,
        HttpContext http,
        CancellationToken cancellationToken) =>
        GenerateAsync(
            new GeneratePdfCommand(
                PdfEngine.SelectPdf,
                request.Template,
                request.Html,
                request.Data ?? default,
                request.Page?.Size,
                request.Page?.Orientation,
                request.Page?.MarginMm,
                request.FileName),
            inline ?? false,
            handler,
            http,
            cancellationToken);

    private static Task<Results<FileContentHttpResult, ProblemHttpResult>> GenerateWordPdfAsync(
        MiniPdfRequest request,
        bool? inline,
        GeneratePdfHandler handler,
        HttpContext http,
        CancellationToken cancellationToken) =>
        GenerateAsync(
            new GeneratePdfCommand(
                PdfEngine.SelectPdfWord,
                request.Template,
                Html: null,
                request.Data ?? default,
                PageSize: null,
                Orientation: null,
                MarginMm: null,
                request.FileName),
            inline ?? false,
            handler,
            http,
            cancellationToken);

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GenerateAsync(
        GeneratePdfCommand command,
        bool inline,
        GeneratePdfHandler handler,
        HttpContext http,
        CancellationToken cancellationToken)
    {
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
}
