namespace Danec.PdfGenerator.Application.Documents.GenerateWord;

public sealed record GenerateWordCommand(string? Template, JsonElement Data, string? FileName);

#pragma warning disable CA1819 // El contenido viaja como byte[] hasta TypedResults.File
public sealed record GeneratedWord(byte[] Content, string FileName, double ElapsedMs);
#pragma warning restore CA1819

/// <summary>Caso de uso: rellena una plantilla Word (MiniWord) y devuelve el .docx para descargar.</summary>
public sealed partial class GenerateWordHandler(
    IWordDocumentGenerator generator,
    TimeProvider timeProvider,
    ILogger<GenerateWordHandler> logger)
{
    private const string InvalidFileNameChars = "\\/:*?\"<>|";

    public async Task<Result<GeneratedWord>> HandleAsync(GenerateWordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var template = TemplateName.Create(command.Template);
        if (template.IsFailure)
        {
            return template.Error;
        }

        var started = timeProvider.GetTimestamp();
        var document = await generator.GenerateAsync(template.Value, command.Data, cancellationToken).ConfigureAwait(false);
        var elapsedMs = timeProvider.GetElapsedTime(started).TotalMilliseconds;

        if (document.IsFailure)
        {
            LogFailed(logger, template.Value.Value, document.Error.Description);
            return document.Error;
        }

        LogGenerated(logger, template.Value.Value, document.Value.Length, elapsedMs);
        return new GeneratedWord(document.Value, BuildFileName(command.FileName, template.Value.Value), Math.Round(elapsedMs, 1));
    }

    private static string BuildFileName(string? requested, string fallback)
    {
        var baseName = string.IsNullOrWhiteSpace(requested) ? fallback : Path.GetFileNameWithoutExtension(requested.Trim());
        var clean = new string([.. baseName.Select(c => char.IsControl(c) || InvalidFileNameChars.Contains(c) ? '_' : c)]).Trim();
        return (clean.Length == 0 ? "documento" : clean) + ".docx";
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Word generado (plantilla {Template}): {Bytes} bytes en {ElapsedMs:F1} ms")]
    private static partial void LogGenerated(ILogger logger, string template, int bytes, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo generar el Word (plantilla {Template}): {Reason}")]
    private static partial void LogFailed(ILogger logger, string template, string reason);
}
