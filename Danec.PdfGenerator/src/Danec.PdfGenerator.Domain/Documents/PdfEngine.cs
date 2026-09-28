namespace Danec.PdfGenerator.Domain.Documents;

/// <summary>Motores de generacion de PDF disponibles.</summary>
public enum PdfEngine
{
    /// <summary>iTextSharp 5.5.13.1 + XMLWorker (AGPL): XHTML con CSS 2.1.</summary>
    IText,

    /// <summary>QuestPDF 2022.12 (ultima MIT): el documento se define en C#, los datos llegan como parametros.</summary>
    QuestPdf,

    /// <summary>HtmlRenderer + PDFsharp (MIT): HTML 4 / CSS 2, 100 % .NET.</summary>
    HtmlRenderer,

    /// <summary>PDF base disenado + texto superpuesto en coordenadas (PDFsharp).</summary>
    Overlay,

    /// <summary>PuppeteerSharp con Chromium headless: HTML5 / CSS3 completo.</summary>
    Puppeteer,

    /// <summary>Microsoft.Playwright con Chromium headless: HTML5 / CSS3 completo.</summary>
    Playwright,

    /// <summary>Plantilla Word (.docx) rellenada con MiniWord y convertida a PDF por Gotenberg (LibreOffice).</summary>
    Docx,
}

/// <summary>Tipo de plantilla que consume cada motor.</summary>
public enum TemplateKind
{
    /// <summary>Archivo .html con sintaxis Scriban.</summary>
    Html,

    /// <summary>PDF base + mapa de campos (.json) con coordenadas.</summary>
    Overlay,

    /// <summary>Documento definido en codigo C# (QuestPDF).</summary>
    Code,

    /// <summary>Documento Word (.docx) con marcadores {{campo}} de MiniWord.</summary>
    Word,
}

public static class PdfEngineExtensions
{
    private static readonly PdfEngine[] HtmlEngines =
        [PdfEngine.IText, PdfEngine.HtmlRenderer, PdfEngine.Puppeteer, PdfEngine.Playwright];

    public static TemplateKind GetTemplateKind(this PdfEngine engine) => engine switch
    {
        PdfEngine.QuestPdf => TemplateKind.Code,
        PdfEngine.Overlay => TemplateKind.Overlay,
        PdfEngine.Docx => TemplateKind.Word,
        _ => TemplateKind.Html,
    };

    /// <summary>Indica si el motor recibe HTML (plantilla Scriban ya renderizada).</summary>
    public static bool UsesHtml(this PdfEngine engine) => engine.GetTemplateKind() == TemplateKind.Html;

    public static IReadOnlyList<PdfEngine> GetEngines(this TemplateKind kind) => kind switch
    {
        TemplateKind.Code => [PdfEngine.QuestPdf],
        TemplateKind.Overlay => [PdfEngine.Overlay],
        TemplateKind.Word => [PdfEngine.Docx],
        _ => HtmlEngines,
    };
}
