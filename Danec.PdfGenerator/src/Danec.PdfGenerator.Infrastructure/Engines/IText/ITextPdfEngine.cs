using System.Text;
using iTextSharp.text.pdf;
using iTextSharp.tool.xml;
using ITextDocument = iTextSharp.text.Document;
using ITextPageSize = iTextSharp.text.PageSize;

namespace Danec.PdfGenerator.Infrastructure.Engines.IText;

/// <summary>
/// iTextSharp 5.5.13.1 + XMLWorker. Convierte XHTML con CSS 2.1 (bloques &lt;style&gt;, clases, bordes,
/// colores, tablas) a PDF. No soporta flexbox, grid, transform, opacity ni position:fixed.
/// LICENCIA AGPL: usarlo exige publicar el codigo fuente de la aplicacion o comprar licencia comercial de iText.
/// </summary>
/// <remarks>
/// El HTML debe estar bien formado (XHTML): etiquetas cerradas, &lt;br /&gt;, atributos entre comillas.
/// Solo trae binarios .NET Framework; en .NET 10 necesita las codificaciones de CodePages (ver DependencyInjection).
/// </remarks>
internal sealed class ITextPdfEngine(IHtmlTemplateRenderer renderer, ILogger<ITextPdfEngine> logger)
    : HtmlPdfEngineBase(renderer, logger)
{
    public override PdfEngine Engine => PdfEngine.IText;

    protected override Task<byte[]> ConvertAsync(string html, PageSettings page, CancellationToken cancellationToken)
    {
        var size = page.Size switch
        {
            PageSize.Letter => ITextPageSize.LETTER,
            PageSize.Legal => ITextPageSize.LEGAL,
            _ => ITextPageSize.A4,
        };
        if (page.IsLandscape)
        {
            size = size.Rotate();
        }

        var margin = (float)page.MarginPoints;
        using var output = new MemoryStream();
        var document = new ITextDocument(size, margin, margin, margin, margin);
        var writer = PdfWriter.GetInstance(document, output);
        writer.CloseStream = false;
        document.Open();

        using (var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(html)))
        {
            XMLWorkerHelper.GetInstance().ParseXHtml(writer, document, htmlStream, Encoding.UTF8);
        }

        document.Close();
        return Task.FromResult(output.ToArray());
    }
}
