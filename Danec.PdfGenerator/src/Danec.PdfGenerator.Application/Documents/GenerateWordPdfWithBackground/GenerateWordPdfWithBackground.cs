using Danec.PdfGenerator.Application.Documents.GeneratePdf;
using Danec.PdfGenerator.Application.Documents.GeneratePdfWithBackground;

namespace Danec.PdfGenerator.Application.Documents.GenerateWord;

public sealed record GenerateWordPdfWithBackgroundCommand(
    string? Template,
    JsonElement Data,
    string? FileName,
    string? Image,
    BackgroundPages? Pages,
    BackgroundFit? Fit);

#pragma warning disable CA1819 // El contenido viaja como byte[] hasta TypedResults.File
/// <param name="Content">PDF final (con el fondo ya estampado).</param>
/// <param name="FileName">Nombre con extension .pdf.</param>
/// <param name="ElapsedMs">Tiempo total: MiniWord + MiniPdf + estampado.</param>
/// <param name="Background">Archivo de imagen usado como fondo.</param>
/// <param name="BackgroundMs">Tiempo del estampado.</param>
public sealed record GeneratedWordPdfWithBackground(
    byte[] Content,
    string FileName,
    double ElapsedMs,
    string Background,
    double BackgroundMs);
#pragma warning restore CA1819

/// <summary>
/// Caso de uso: plantilla Word -> .docx (MiniWord) -> PDF (MiniPdf) -> imagen de fondo detras del contenido.
/// El paso Word -> PDF lo hace el motor <see cref="PdfEngine.MiniPdf"/> a traves de <see cref="GeneratePdfHandler"/>;
/// el estampador (PDFsharp) solo acepta PDF, nunca el .docx.
/// </summary>
public sealed partial class GenerateWordPdfWithBackgroundHandler(
    GeneratePdfHandler pdfHandler,
    IBackgroundImageStore images,
    IPdfBackgroundStamper stamper,
    TimeProvider timeProvider,
    ILogger<GenerateWordPdfWithBackgroundHandler> logger)
{
    public async Task<Result<GeneratedWordPdfWithBackground>> HandleAsync(
        GenerateWordPdfWithBackgroundCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. El fondo se valida y se carga antes de generar, para no gastar la conversion si no existe
        var spec = BackgroundSpec.Create(command.Image, images.DefaultName, command.Pages, command.Fit);
        if (spec.IsFailure)
        {
            return spec.Error;
        }

        var image = await images.GetAsync(spec.Value.Image, cancellationToken).ConfigureAwait(false);
        if (image.IsFailure)
        {
            return image.Error;
        }

        // 2. MiniWord rellena el .docx y MiniPdf lo convierte a PDF (tamano y margenes los define el .docx)
        var pdf = await pdfHandler.HandleAsync(
            new GeneratePdfCommand(
                PdfEngine.MiniPdf,
                command.Template,
                Html: null,
                command.Data,
                PageSize: null,
                Orientation: null,
                MarginMm: null,
                command.FileName),
            cancellationToken).ConfigureAwait(false);
        if (pdf.IsFailure)
        {
            return pdf.Error;
        }

        // 3. Imagen de fondo detras del contenido
        using var activity = PdfTelemetry.ActivitySource.StartActivity("pdf.background");
        activity?.SetTag("pdf.background", image.Value.FileName);

        var started = timeProvider.GetTimestamp();
        var stamped = stamper.Stamp(pdf.Value.Content, image.Value, spec.Value);
        var backgroundMs = Math.Round(timeProvider.GetElapsedTime(started).TotalMilliseconds, 1);

        if (stamped.IsFailure)
        {
            LogFailed(logger, image.Value.FileName, stamped.Error.Description);
            return stamped.Error;
        }

        LogStamped(logger, image.Value.FileName, spec.Value.Pages, spec.Value.Fit, backgroundMs);
        return new GeneratedWordPdfWithBackground(
            stamped.Value,
            pdf.Value.FileName,
            Math.Round(pdf.Value.ElapsedMs + backgroundMs, 1),
            image.Value.FileName,
            backgroundMs);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Fondo {Background} aplicado al PDF de Word (paginas {Pages}, ajuste {Fit}) en {ElapsedMs:F1} ms")]
    private static partial void LogStamped(ILogger logger, string background, BackgroundPages pages, BackgroundFit fit, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo aplicar el fondo {Background} al PDF de Word: {Reason}")]
    private static partial void LogFailed(ILogger logger, string background, string reason);
}
