using Danec.PdfGenerator.Infrastructure.Templates;
using MiniSoftware;

namespace Danec.PdfGenerator.Infrastructure.Engines.Docx;

/// <summary>
/// MiniWord (Apache 2.0): reemplaza los marcadores {{campo}} de Templates/docx/{nombre}.docx.
/// Una fila de tabla con {{items.campo}} se repite por cada elemento del arreglo "items".
/// Importante: en Word, cada marcador debe escribirse de una sola vez (sin cambiar formato a mitad),
/// para que quede en un unico "run" y MiniWord lo encuentre.
/// </summary>
internal sealed partial class MiniWordDocumentGenerator(
    TemplatePaths paths,
    IEnumerable<IWordTemplateEnricher> enrichers,
    ILogger<MiniWordDocumentGenerator> logger) : IWordDocumentGenerator
{
    public async Task<Result<byte[]>> GenerateAsync(TemplateName template, JsonElement data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(template);

        var file = paths.DocxFile(template);
        if (!File.Exists(file))
        {
            return DocumentErrors.TemplateNotFound(template.Value, TemplateKind.Word);
        }

        var templateBytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);

        Dictionary<string, object> model;
        try
        {
            model = WordTemplateModel.FromJson(data);
            foreach (var enricher in enrichers.Where(e => string.Equals(e.Name, template.Value, StringComparison.Ordinal)))
            {
                enricher.Enrich(data, model);
            }
        }
        catch (JsonException ex)
        {
            return DocumentErrors.InvalidData(ex.Message);
        }

        try
        {
            using var output = new MemoryStream();
            output.SaveAsByTemplate(templateBytes, model);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, template.Value);
            return DocumentErrors.WordGenerationFailed(ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "MiniWord fallo al rellenar la plantilla {Template}")]
    private static partial void LogFailed(ILogger logger, Exception exception, string template);
}
