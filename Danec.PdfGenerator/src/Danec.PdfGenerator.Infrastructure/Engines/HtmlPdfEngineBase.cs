namespace Danec.PdfGenerator.Infrastructure.Engines;

/// <summary>
/// Plantilla comun de los motores HTML (iText, HtmlRenderer, Puppeteer, Playwright):
/// 1) Scriban combina la plantilla con los parametros; 2) el motor convierte el HTML a PDF.
/// </summary>
internal abstract partial class HtmlPdfEngineBase(IHtmlTemplateRenderer renderer, ILogger logger) : IPdfEngine
{
    public abstract PdfEngine Engine { get; }

    public async Task<Result<byte[]>> RenderAsync(PdfRenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var html = await renderer.RenderAsync(request.Template, request.Data, Engine, cancellationToken).ConfigureAwait(false);
        if (html.IsFailure)
        {
            return html.Error;
        }

        try
        {
            return await ConvertAsync(html.Value, request.Page, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogConversionFailed(logger, ex, Engine);
            return DocumentErrors.RenderFailed(Engine, ex.Message);
        }
    }

    protected abstract Task<byte[]> ConvertAsync(string html, PageSettings page, CancellationToken cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo la conversion HTML a PDF con {Engine}")]
    private static partial void LogConversionFailed(ILogger logger, Exception exception, PdfEngine engine);
}
