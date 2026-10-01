using Danec.PdfGenerator.Application.Documents.GeneratePdf;

namespace Danec.PdfGenerator.Application.Documents.GeneratePdfWithBackground;

#pragma warning disable CA1819 // Los bytes viajan como byte[] hasta TypedResults.File
/// <summary>Imagen de fondo del catalogo (PNG o JPEG ya validado).</summary>
public sealed record BackgroundImage(string Name, string FileName, byte[] Content);
#pragma warning restore CA1819

/// <summary>Catalogo de imagenes de fondo (Templates/fondo).</summary>
public interface IBackgroundImageStore
{
    /// <summary>Imagen que se usa cuando la solicitud no indica 'fondo.imagen'.</summary>
    string DefaultName { get; }

    Task<Result<BackgroundImage>> GetAsync(TemplateName name, CancellationToken cancellationToken);
}

/// <summary>Estampa una imagen DETRAS del contenido de un PDF existente.</summary>
public interface IPdfBackgroundStamper
{
    Result<byte[]> Stamp(byte[] pdf, BackgroundImage image, BackgroundSpec spec);
}

/// <summary>Generacion normal + datos del fondo.</summary>
/// <param name="Pdf">Mismo comando que /pdf/{motor}.</param>
/// <param name="Image">Nombre de la imagen; null usa la imagen por defecto.</param>
/// <param name="Pages">Todas (defecto) o Primera.</param>
/// <param name="Fit">Ancho (defecto), Pagina o Centrado.</param>
public sealed record GeneratePdfWithBackgroundCommand(
    GeneratePdfCommand Pdf,
    string? Image,
    BackgroundPages? Pages,
    BackgroundFit? Fit);

public sealed record GeneratedPdfWithBackground(GeneratedPdf Pdf, string Background, double BackgroundMs);

/// <summary>
/// Caso de uso: genera el PDF con el motor indicado y luego estampa la imagen de fondo detras del
/// contenido. El fondo se valida y se carga ANTES de generar, para no gastar el render si no existe.
/// </summary>
public sealed partial class GeneratePdfWithBackgroundHandler(
    GeneratePdfHandler pdfHandler,
    IBackgroundImageStore images,
    IPdfBackgroundStamper stamper,
    TimeProvider timeProvider,
    ILogger<GeneratePdfWithBackgroundHandler> logger)
{
    public async Task<Result<GeneratedPdfWithBackground>> HandleAsync(
        GeneratePdfWithBackgroundCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

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

        var pdf = await pdfHandler.HandleAsync(command.Pdf, cancellationToken).ConfigureAwait(false);
        if (pdf.IsFailure)
        {
            return pdf.Error;
        }

        using var activity = PdfTelemetry.ActivitySource.StartActivity("pdf.background");
        activity?.SetTag("pdf.background", image.Value.FileName);

        var started = timeProvider.GetTimestamp();
        var stamped = stamper.Stamp(pdf.Value.Content, image.Value, spec.Value);
        var elapsedMs = Math.Round(timeProvider.GetElapsedTime(started).TotalMilliseconds, 1);

        if (stamped.IsFailure)
        {
            LogFailed(logger, image.Value.FileName, stamped.Error.Description);
            return stamped.Error;
        }

        LogStamped(logger, image.Value.FileName, spec.Value.Pages, spec.Value.Fit, elapsedMs);
        return new GeneratedPdfWithBackground(
            pdf.Value with { Content = stamped.Value },
            image.Value.FileName,
            elapsedMs);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Fondo {Background} aplicado (paginas {Pages}, ajuste {Fit}) en {ElapsedMs:F1} ms")]
    private static partial void LogStamped(ILogger logger, string background, BackgroundPages pages, BackgroundFit fit, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo aplicar el fondo {Background}: {Reason}")]
    private static partial void LogFailed(ILogger logger, string background, string reason);
}
