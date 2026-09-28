using TheArtOfDev.HtmlRenderer.PdfSharp;
using HtmlPdfGenerator = TheArtOfDev.HtmlRenderer.PdfSharp.PdfGenerator;
using SharpOrientation = PdfSharp.PageOrientation;
using SharpPageSize = PdfSharp.PageSize;

namespace Danec.PdfGenerator.Infrastructure.Engines.HtmlRenderer;

/// <summary>
/// HtmlRenderer.PdfSharp (BSD) sobre PDFsharp 6 (MIT). 100 % .NET, sin navegador.
/// Soporta HTML 4.01 y CSS 2: tablas, colores, bordes, fuentes. No soporta flexbox, grid ni @page.
/// </summary>
internal sealed class HtmlRendererPdfEngine(IHtmlTemplateRenderer renderer, ILogger<HtmlRendererPdfEngine> logger)
    : HtmlPdfEngineBase(renderer, logger)
{
    public override PdfEngine Engine => PdfEngine.HtmlRenderer;

    protected override Task<byte[]> ConvertAsync(string html, PageSettings page, CancellationToken cancellationToken)
    {
        var config = new PdfGenerateConfig
        {
            PageSize = page.Size switch
            {
                PageSize.Letter => SharpPageSize.Letter,
                PageSize.Legal => SharpPageSize.Legal,
                _ => SharpPageSize.A4,
            },
            PageOrientation = page.IsLandscape ? SharpOrientation.Landscape : SharpOrientation.Portrait,
        };
        config.SetMargins((int)Math.Round(page.MarginPoints));

        using var document = HtmlPdfGenerator.GeneratePdf(html, config);
        using var output = new MemoryStream();
        document.Save(output);
        return Task.FromResult(output.ToArray());
    }
}
