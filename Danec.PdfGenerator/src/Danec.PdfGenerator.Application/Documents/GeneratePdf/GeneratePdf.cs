using System.Diagnostics;

namespace Danec.PdfGenerator.Application.Documents.GeneratePdf;

public sealed record GeneratePdfCommand(
    PdfEngine Engine,
    string? Template,
    string? Html,
    JsonElement Data,
    PageSize? PageSize,
    PageOrientation? Orientation,
    double? MarginMm,
    string? FileName);

#pragma warning disable CA1819 // El contenido del PDF viaja como byte[] hasta TypedResults.File
public sealed record GeneratedPdf(byte[] Content, string FileName, PdfEngine Engine, double ElapsedMs);
#pragma warning restore CA1819

/// <summary>Caso de uso: valida la solicitud, elige el motor y genera el PDF.</summary>
public sealed partial class GeneratePdfHandler(
    IPdfEngineFactory engines,
    TimeProvider timeProvider,
    ILogger<GeneratePdfHandler> logger)
{
    private const string InvalidFileNameChars = "\\/:*?\"<>|";

    public async Task<Result<GeneratedPdf>> HandleAsync(GeneratePdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var source = TemplateSource.Create(command.Template, command.Html);
        if (source.IsFailure)
        {
            return source.Error;
        }

        var supported = source.Value.EnsureSupportedBy(command.Engine);
        if (supported.IsFailure)
        {
            return supported.Error;
        }

        var page = PageSettings.Create(command.PageSize, command.Orientation, command.MarginMm);
        if (page.IsFailure)
        {
            return page.Error;
        }

        if (engines.Get(command.Engine) is not { } engine)
        {
            return DocumentErrors.EngineUnavailable(command.Engine);
        }

        using var activity = PdfTelemetry.ActivitySource.StartActivity("pdf.generate");
        activity?.SetTag("pdf.engine", command.Engine.ToString());
        activity?.SetTag("pdf.template", source.Value.DisplayName);

        var tags = new TagList { { "pdf.engine", command.Engine.ToString() } };
        var started = timeProvider.GetTimestamp();

        var rendered = await engine
            .RenderAsync(new PdfRenderRequest(source.Value, command.Data, page.Value), cancellationToken)
            .ConfigureAwait(false);

        var elapsedMs = timeProvider.GetElapsedTime(started).TotalMilliseconds;

        if (rendered.IsFailure)
        {
            PdfTelemetry.RenderFailures.Add(1, tags);
            activity?.SetStatus(ActivityStatusCode.Error, rendered.Error.Code);
            LogFailed(logger, command.Engine, source.Value.DisplayName, rendered.Error.Description);
            return rendered.Error;
        }

        PdfTelemetry.RenderDuration.Record(elapsedMs, tags);
        LogGenerated(logger, command.Engine, source.Value.DisplayName, rendered.Value.Length, elapsedMs);

        return new GeneratedPdf(
            rendered.Value,
            BuildFileName(command.FileName, source.Value),
            command.Engine,
            Math.Round(elapsedMs, 1));
    }

    private static string BuildFileName(string? requested, TemplateSource source)
    {
        var baseName = string.IsNullOrWhiteSpace(requested)
            ? source.DisplayName
            : Path.GetFileNameWithoutExtension(requested.Trim());

        var clean = new string([.. baseName.Select(c => char.IsControl(c) || InvalidFileNameChars.Contains(c) ? '_' : c)]).Trim();
        return (clean.Length == 0 ? "documento" : clean) + ".pdf";
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "PDF generado con {Engine} (plantilla {Template}): {Bytes} bytes en {ElapsedMs:F1} ms")]
    private static partial void LogGenerated(ILogger logger, PdfEngine engine, string template, int bytes, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo generar el PDF con {Engine} (plantilla {Template}): {Reason}")]
    private static partial void LogFailed(ILogger logger, PdfEngine engine, string template, string reason);
}
