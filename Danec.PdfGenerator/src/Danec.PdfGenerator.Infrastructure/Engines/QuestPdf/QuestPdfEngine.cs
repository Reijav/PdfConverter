using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Danec.PdfGenerator.Infrastructure.Engines.QuestPdf;

/// <summary>Plantilla QuestPDF: el diseno vive en C# y recibe los parametros JSON.</summary>
internal interface IQuestPdfTemplate
{
    string Name { get; }

    Result<IDocument> Create(JsonElement data, PageSettings page);
}

/// <summary>
/// QuestPDF 2022.12.15, la ultima version publicada con licencia MIT (sin soporte oficial).
/// No convierte HTML: cada plantilla es una clase <see cref="IQuestPdfTemplate"/>.
/// </summary>
internal sealed partial class QuestPdfEngine(IEnumerable<IQuestPdfTemplate> templates, ILogger<QuestPdfEngine> logger) : IPdfEngine
{
    public PdfEngine Engine => PdfEngine.QuestPdf;

    public Task<Result<byte[]>> RenderAsync(PdfRenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Template.DisplayName;
        var template = templates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
        if (template is null)
        {
            return Task.FromResult<Result<byte[]>>(DocumentErrors.TemplateNotFound(name, TemplateKind.Code));
        }

        var document = template.Create(request.Data, request.Page);
        if (document.IsFailure)
        {
            return Task.FromResult<Result<byte[]>>(document.Error);
        }

        try
        {
            return Task.FromResult<Result<byte[]>>(document.Value.GeneratePdf());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, name);
            return Task.FromResult<Result<byte[]>>(DocumentErrors.RenderFailed(Engine, ex.Message));
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "QuestPDF fallo al generar la plantilla {Template}")]
    private static partial void LogFailed(ILogger logger, Exception exception, string template);
}
