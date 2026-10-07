using Danec.PdfGenerator.Infrastructure.Engines.Docx;
using WordToPdf = SelectPdf.Universal.WordToPdf;

namespace Danec.PdfGenerator.Infrastructure.Engines.SelectPdfUniversal;

/// <summary>
/// Plantilla Word sin servicios externos: MiniWord rellena el .docx y SelectPdf.Universal (licencia comercial)
/// lo convierte a PDF dentro del proceso, sin navegador. Tamano y margenes los define el .docx.
/// La conversion Word -> PDF no existe en la edicion gratuita: sin Pdf:SelectPdf:LicenseKey sale con marca de agua de prueba.
/// </summary>
internal sealed partial class SelectPdfDocxEngine(
    IWordDocumentGenerator word,
    IOptions<PdfGeneratorOptions> options,
    ILogger<SelectPdfDocxEngine> logger) : IPdfEngine, IDisposable
{
    private readonly SemaphoreSlim _limiter = new(options.Value.SelectPdf.MaxConcurrency, options.Value.SelectPdf.MaxConcurrency);

    public PdfEngine Engine => PdfEngine.SelectPdfWord;

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
            var document = new WordToPdf().ConvertBytes(docx.Value);
            try
            {
                return document.Save();
            }
            finally
            {
                document.Close();
            }
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

    [LoggerMessage(Level = LogLevel.Error, Message = "SelectPdf fallo al convertir la plantilla Word {Template}")]
    private static partial void LogFailed(ILogger logger, Exception exception, string template);
}
