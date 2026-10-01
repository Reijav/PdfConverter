using Danec.PdfGenerator.Application.Documents.GeneratePdfWithBackground;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace Danec.PdfGenerator.Infrastructure.Backgrounds;

/// <summary>
/// Estampa la imagen con PDFsharp (MIT) usando <see cref="XGraphicsPdfPageOptions.Prepend"/>: el dibujo se
/// inserta al INICIO del flujo de contenido de la pagina, por lo que queda debajo del texto, bordes y
/// rellenos de tabla. La imagen se incrusta una sola vez y todas las paginas la referencian.
/// </summary>
internal sealed partial class PdfSharpBackgroundStamper(ILogger<PdfSharpBackgroundStamper> logger) : IPdfBackgroundStamper
{
    public Result<byte[]> Stamp(byte[] pdf, BackgroundImage image, BackgroundSpec spec)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(spec);

        try
        {
            using var input = new MemoryStream(pdf, writable: false);
            using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            using var imageStream = new MemoryStream(image.Content, writable: false);
            using var xImage = XImage.FromStream(imageStream);

            var pageCount = spec.Pages == BackgroundPages.Primera ? Math.Min(1, document.PageCount) : document.PageCount;
            for (var i = 0; i < pageCount; i++)
            {
                using var gfx = XGraphics.FromPdfPage(document.Pages[i], XGraphicsPdfPageOptions.Prepend);
                gfx.DrawImage(xImage, Place(gfx.PageSize, xImage, spec.Fit));
            }

            using var output = new MemoryStream();
            document.Save(output);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, image.FileName);
            return DocumentErrors.BackgroundFailed(ex.Message);
        }
    }

    /// <summary>Rectangulo destino en puntos, origen arriba a la izquierda.</summary>
    internal static XRect Place(XSize page, XImage image, BackgroundFit fit)
    {
        var ratio = (double)image.PixelHeight / image.PixelWidth;
        switch (fit)
        {
            case BackgroundFit.Pagina:
                return new XRect(0, 0, page.Width, page.Height);

            case BackgroundFit.Centrado:
                var width = image.PointWidth;
                var height = image.PointHeight;
                var scale = Math.Min(1, Math.Min(page.Width / width, page.Height / height));
                width *= scale;
                height *= scale;
                return new XRect((page.Width - width) / 2, (page.Height - height) / 2, width, height);

            default:
                return new XRect(0, 0, page.Width, page.Width * ratio);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo el estampado del fondo {Background}")]
    private static partial void LogFailed(ILogger logger, Exception exception, string background);
}
