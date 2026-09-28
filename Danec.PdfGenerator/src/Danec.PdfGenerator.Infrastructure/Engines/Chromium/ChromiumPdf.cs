namespace Danec.PdfGenerator.Infrastructure.Engines.Chromium;

/// <summary>Valores compartidos por PuppeteerSharp y Playwright (mismo motor de impresion de Chromium).</summary>
internal static class ChromiumPdf
{
    public const string HeaderTemplate = "<span></span>";

    public const string FooterTemplate =
        "<div style=\"width:100%;font-size:8px;color:#777;text-align:center;font-family:Arial,sans-serif\">" +
        "Página <span class=\"pageNumber\"></span> de <span class=\"totalPages\"></span></div>";

    public const string WaitForFonts = "() => document.fonts.ready.then(() => true)";

    public static string Margin(PageSettings page) => $"{page.MarginMm.ToString("0.##", CultureInfo.InvariantCulture)}mm";

    /// <summary>Solo se permiten recursos embebidos: evita que una plantilla haga peticiones a la red interna (SSRF).</summary>
    public static bool IsEmbeddedResource(string url) =>
        url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("about:", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Encuentra el Chromium que instala Playwright para que Puppeteer lo reutilice (un solo navegador en disco).</summary>
internal static class ChromiumLocator
{
    public static string? FindPlaywrightChromium()
    {
        var root = PlaywrightBrowsersRoot();
        if (!Directory.Exists(root))
        {
            return null;
        }

        var executable = OperatingSystem.IsWindows() ? "chrome.exe" : "chrome";
        return Directory.EnumerateDirectories(root, "chromium-*")
            .Select(dir => (dir, revision: int.TryParse(Path.GetFileName(dir).AsSpan("chromium-".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var r) ? r : 0))
            .OrderByDescending(x => x.revision)
            .SelectMany(x => Directory.EnumerateFiles(x.dir, executable, SearchOption.AllDirectories))
            .FirstOrDefault();
    }

    private static string PlaywrightBrowsersRoot()
    {
        var configured = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && configured != "0")
        {
            return configured;
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Caches", "ms-playwright")
            : Path.Combine(home, ".cache", "ms-playwright");
    }
}
