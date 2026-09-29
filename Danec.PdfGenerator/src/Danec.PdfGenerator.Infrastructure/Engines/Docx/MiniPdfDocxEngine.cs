namespace Danec.PdfGenerator.Infrastructure.Engines.Docx;

/// <summary>
/// Motor "minipdf": MiniWord rellena la plantilla Word y MiniPdf (Apache 2.0) la convierte a PDF dentro del proceso,
/// sin Gotenberg. El tamano de pagina y los margenes los define el propio .docx.
/// Limitaciones conocidas de MiniPdf 0.x: no resuelve campos de Word (PAGE / NUMPAGES), no dibuja bordes de parrafo
/// y no genera PDF/A. Las fuentes para Linux se registran al iniciar (ver MiniPdfFontSetup).
/// </summary>
internal sealed partial class MiniPdfDocxEngine(
    IWordDocumentGenerator word,
    IOptions<PdfGeneratorOptions> options,
    ILogger<MiniPdfDocxEngine> logger) : IPdfEngine, IDisposable
{
    // La conversion es sincrona y usa CPU: se limita la concurrencia para acotar memoria
    private readonly SemaphoreSlim _limiter = new(options.Value.MiniPdf.MaxConcurrency, options.Value.MiniPdf.MaxConcurrency);

    public PdfEngine Engine => PdfEngine.MiniPdf;

    public async Task<Result<byte[]>> RenderAsync(PdfRenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Template.Name is not { } name)
        {
            return DocumentErrors.InlineHtmlNotSupported(Engine);
        }

        var docx = await word.GenerateAsync(name, request.Data, cancellationToken).ConfigureAwait(false);
        if (docx.IsFailure)
        {
            return docx.Error;
        }

        await _limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var input = new MemoryStream(docx.Value, writable: false);
            return MiniSoftware.MiniPdf.ConvertToPdf(input);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, name.Value);
            return DocumentErrors.RenderFailed(Engine, ex.Message);
        }
        finally
        {
            _limiter.Release();
        }
    }

    public void Dispose() => _limiter.Dispose();

    [LoggerMessage(Level = LogLevel.Error, Message = "MiniPdf fallo al convertir la plantilla {Template}")]
    private static partial void LogFailed(ILogger logger, Exception exception, string template);
}
