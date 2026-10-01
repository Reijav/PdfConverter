using System.ComponentModel.DataAnnotations;

namespace Danec.PdfGenerator.Infrastructure.Settings;

/// <summary>Seccion "Pdf" de appsettings.</summary>
public sealed class PdfGeneratorOptions
{
    public const string SectionName = "Pdf";

    /// <summary>Carpeta de plantillas (relativa a la app o absoluta). Contiene html/, overlay/ y samples/.</summary>
    [Required]
    public string TemplatesPath { get; set; } = "Templates";

    /// <summary>Carpeta adicional de fuentes .ttf para PDFsharp (HtmlRenderer y Overlay).</summary>
    public string? FontsPath { get; set; }

    /// <summary>Fuente por defecto de QuestPDF y Overlay. En Linux usa "Liberation Sans".</summary>
    [Required]
    public string DefaultFontFamily { get; set; } = "Arial";

    public ChromiumOptions Chromium { get; set; } = new();

    public GotenbergOptions Gotenberg { get; set; } = new();

    public MiniPdfOptions MiniPdf { get; set; } = new();

    public BackgroundOptions Fondo { get; set; } = new();
}

/// <summary>Imagenes de fondo que se estampan detras del PDF (Templates/fondo/{nombre}.png|.jpg|.jpeg).</summary>
public sealed class BackgroundOptions
{
    /// <summary>Imagen usada cuando la solicitud no envia 'fondo.imagen' (nombre sin extension).</summary>
    public string Default { get; set; } = "fondo-de-cartas";

    /// <summary>Tamano maximo de la imagen. Cada PDF incrusta la imagen completa.</summary>
    public int MaxBytes { get; set; } = 5 * 1024 * 1024;
}

/// <summary>MiniPdf (Apache 2.0): convierte el .docx de MiniWord a PDF dentro del proceso, sin Gotenberg.</summary>
public sealed class MiniPdfOptions
{
    /// <summary>Conversiones simultaneas (la conversion usa CPU y memoria del proceso de la API).</summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Familia de Word -> archivo .ttf (absoluto o relativo a Pdf:FontsPath), p.ej. "Arial": "/fonts/arial.ttf".
    /// Fuera de Windows, las familias no configuradas se mapean a Liberation si esta instalada.
    /// </summary>
    public Dictionary<string, string> Fonts { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Gotenberg (contenedor gotenberg/gotenberg:8): convierte el .docx de MiniWord a PDF con LibreOffice.</summary>
public sealed class GotenbergOptions
{
    /// <summary>
    /// URL de Gotenberg, que se inicia con --api-port=3001. Local: http://localhost:3001;
    /// docker-compose: http://gotenberg:3001; sidecar en Container Apps: http://localhost:3001.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:3001";

    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>Configuracion compartida por PuppeteerSharp y Playwright.</summary>
public sealed class ChromiumOptions
{
    /// <summary>Ruta a chrome/chromium. Vacio: Playwright usa su navegador; Puppeteer reutiliza el de Playwright o descarga Chrome.</summary>
    public string? ExecutablePath { get; set; }

    /// <summary>Carpeta donde Puppeteer descarga Chrome si no encuentra uno instalado.</summary>
    public string? DownloadPath { get; set; }

    /// <summary>PDFs simultaneos por motor. Cada pestana de Chromium consume ~50-100 MB.</summary>
    public int MaxConcurrency { get; set; } = 4;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// false (recomendado): se bloquea toda peticion de red de la plantilla (solo data: URIs).
    /// Evita SSRF cuando el HTML o los parametros vienen del cliente.
    /// </summary>
    public bool AllowExternalResources { get; set; }

#pragma warning disable CA1819 // Arreglo para que la configuracion reemplace (no agregue) los valores por defecto
    public string[] Args { get; set; } = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu"];
#pragma warning restore CA1819
}
