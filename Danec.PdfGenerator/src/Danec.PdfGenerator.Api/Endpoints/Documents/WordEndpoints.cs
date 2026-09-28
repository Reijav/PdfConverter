using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Danec.PdfGenerator.Application.Documents.GenerateWord;
using Danec.PdfGenerator.Domain.Documents;
using Microsoft.Extensions.Validation;

namespace Danec.PdfGenerator.Api.Endpoints.Documents;

#pragma warning disable ASP0029 // [SkipValidation] es experimental; el validador de .NET 10 no soporta JsonElement

/// <summary>Solicitud para generar un documento Word.</summary>
/// <param name="Template">Plantilla Word: Templates/docx/{template}.docx.</param>
/// <param name="Data">Parametros. Objetos anidados se escriben como {{padre_hijo}}; arreglos como filas {{lista.campo}}.</param>
/// <param name="FileName">Nombre del archivo devuelto (sin ruta).</param>
public sealed record GenerateWordRequest(
    [StringLength(TemplateName.MaxLength)] string? Template,
    [SkipValidation] JsonElement? Data,
    [StringLength(120)] string? FileName);

#pragma warning restore ASP0029

/// <summary>Descarga del documento Word generado con MiniWord (sin convertir a PDF).</summary>
public static class WordEndpoints
{
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static IEndpointRouteBuilder MapWordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/word", GenerateAsync)
            .WithTags("Word")
            .WithName("GenerateWord")
            .WithSummary("MiniWord: plantilla Word + parametros -> .docx")
            .WithDescription("Rellena Templates/docx/{template}.docx con los parametros y devuelve el Word editable. Para el PDF use POST /api/v1/pdf/docx.")
            .Produces(StatusCodes.Status200OK, contentType: DocxContentType)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GenerateAsync(
        GenerateWordRequest request,
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
        return TypedResults.File(result.Value.Content, DocxContentType, result.Value.FileName);
    }
}
