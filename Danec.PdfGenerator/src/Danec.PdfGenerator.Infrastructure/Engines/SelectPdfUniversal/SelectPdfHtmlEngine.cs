using HtmlToPdf = SelectPdf.Universal.HtmlToPdf;
using SelectOrientation = SelectPdf.Universal.PdfPageOrientation;
using SelectPageSize = SelectPdf.Universal.PdfPageSize;

namespace Danec.PdfGenerator.Infrastructure.Engines.SelectPdfUniversal;

/// <summary>
/// SelectPdf.Universal (licencia comercial): HTML5 / CSS3 con el Chromium que trae el paquete
/// SelectPdf.Universal.Native.{rid}. Sin licencia (Pdf:SelectPdf:LicenseKey) el PDF sale con marca de agua de prueba.
/// No estampa imagen de fondo: el PDF se devuelve tal como lo genera la libreria.
/// </summary>
internal sealed class SelectPdfHtmlEngine(
    IHtmlTemplateRenderer renderer,
    IOptions<PdfGeneratorOptions> options,
    ILogger<SelectPdfHtmlEngine> logger) : HtmlPdfEngineBase(renderer, logger), IDisposable
{
    /// <summary>Pixeles CSS por punto PDF (96 px y 72 pt por pulgada).</summary>
    private const double PixelsPerPoint = 96d / 72d;

    private readonly SemaphoreSlim _limiter = new(options.Value.SelectPdf.MaxConcurrency, options.Value.SelectPdf.MaxConcurrency);

    public override PdfEngine Engine => PdfEngine.SelectPdf;

    protected override async Task<byte[]> ConvertAsync(string html, PageSettings page, CancellationToken cancellationToken)
    {
        var margin = (int)Math.Round(page.MarginPoints);

        var converter = new HtmlToPdf();
        converter.Options.PdfPageSize = page.Size switch
        {
            PageSize.Letter => SelectPageSize.Letter,
            PageSize.Legal => SelectPageSize.Legal,
            _ => SelectPageSize.A4,
        };
        converter.Options.PdfPageOrientation = page.IsLandscape ? SelectOrientation.Landscape : SelectOrientation.Portrait;
        converter.Options.MarginTop = margin;
        converter.Options.MarginBottom = margin;
        converter.Options.MarginLeft = margin;
        converter.Options.MarginRight = margin;
        // SelectPdf dibuja el HTML en una ventana de WebPageWidth pixeles (1024 por defecto) y la encoge hasta el ancho
        // de la pagina: con 1024 todo sale a ~2/3 de su tamano. Con el ancho util de la pagina la escala es 1:1 (10pt = 10pt).
        converter.Options.WebPageWidth = PrintableWidthPixels(page, margin);
        converter.Options.NavigationTimeout = options.Value.SelectPdf.TimeoutSeconds;
        // Las plantillas son HTML estatico: sin scripts un parametro nunca puede ejecutar codigo en Chromium
        converter.Options.JavaScriptEnabled = false;

        await _limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await converter.ConvertHtmlStringAsync(html, cancellationToken).ConfigureAwait(false);
            try
            {
                return document.Save();
            }
            finally
            {
                document.Close();
            }
        }
        finally
        {
            _limiter.Release();
        }
    }

    public void Dispose() => _limiter.Dispose();

    /// <summary>Ancho util de la pagina (sin margenes) en pixeles CSS.</summary>
    private static int PrintableWidthPixels(PageSettings page, int marginPoints)
    {
        var (width, height) = page.Size switch
        {
            PageSize.Letter => (612d, 792d),
            PageSize.Legal => (612d, 1008d),
            _ => (595.28d, 841.89d),
        };

        var pageWidth = page.IsLandscape ? height : width;
        return Math.Max(1, (int)Math.Round((pageWidth - (2 * marginPoints)) * PixelsPerPoint));
    }
}
