using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Danec.PdfGenerator.Infrastructure.Engines.Chromium;

/// <summary>
/// Un solo Chromium por proceso (lanzarlo cuesta 0.5-2 s). Cada PDF usa un contexto aislado
/// y <see cref="Slots"/> limita cuantas pestanas hay abiertas a la vez.
/// </summary>
internal sealed partial class PuppeteerBrowserProvider(IOptions<PdfGeneratorOptions> options, ILogger<PuppeteerBrowserProvider> logger)
    : IAsyncDisposable
{
    private readonly SemaphoreSlim _launchLock = new(1, 1);
    private IBrowser? _browser;

    public SemaphoreSlim Slots { get; } = new(options.Value.Chromium.MaxConcurrency, options.Value.Chromium.MaxConcurrency);

    public async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true, IsClosed: false } ready)
        {
            return ready;
        }

        await _launchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_browser is { IsConnected: true, IsClosed: false } current)
            {
                return current;
            }

            if (_browser is not null)
            {
                await _browser.DisposeAsync().ConfigureAwait(false);
            }

            var settings = options.Value.Chromium;
            var executable = settings.ExecutablePath
                ?? ChromiumLocator.FindPlaywrightChromium()
                ?? await DownloadChromeAsync(settings).ConfigureAwait(false);

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = executable,
                Args = settings.Args,
                Timeout = settings.TimeoutSeconds * 1000,
            }).ConfigureAwait(false);

            LogLaunched(logger, executable);
            return _browser;
        }
        finally
        {
            _launchLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
        }

        _launchLock.Dispose();
        Slots.Dispose();
    }

    private async Task<string> DownloadChromeAsync(ChromiumOptions settings)
    {
        var path = settings.DownloadPath ?? Path.Combine(Path.GetTempPath(), "danec-pdfgenerator", "puppeteer");
        LogDownloading(logger, path);
        var fetcher = new BrowserFetcher(new BrowserFetcherOptions { Browser = SupportedBrowser.Chrome, Path = path });
        var installed = await fetcher.DownloadAsync().ConfigureAwait(false);
        return installed.GetExecutablePath();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "PuppeteerSharp: Chromium iniciado desde {Executable}")]
    private static partial void LogLaunched(ILogger logger, string executable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PuppeteerSharp: no se encontro Chromium; descargando Chrome en {Path}")]
    private static partial void LogDownloading(ILogger logger, string path);
}

/// <summary>PuppeteerSharp (MIT): Chromium headless, HTML5/CSS3 completo, encabezado y pie con numeracion.</summary>
internal sealed partial class PuppeteerPdfEngine(
    IHtmlTemplateRenderer renderer,
    PuppeteerBrowserProvider browsers,
    IOptions<PdfGeneratorOptions> options,
    ILogger<PuppeteerPdfEngine> logger) : HtmlPdfEngineBase(renderer, logger)
{
    public override PdfEngine Engine => PdfEngine.Puppeteer;

    protected override async Task<byte[]> ConvertAsync(string html, PageSettings page, CancellationToken cancellationToken)
    {
        var settings = options.Value.Chromium;
        var timeoutMs = settings.TimeoutSeconds * 1000;

        await browsers.Slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var browser = await browsers.GetBrowserAsync(cancellationToken).ConfigureAwait(false);
            await using var context = await browser.CreateBrowserContextAsync().ConfigureAwait(false);
            await using var tab = await context.NewPageAsync().ConfigureAwait(false);
            tab.DefaultTimeout = timeoutMs;

            if (!settings.AllowExternalResources)
            {
                await tab.SetRequestInterceptionAsync(true).ConfigureAwait(false);
                tab.Request += async (_, e) =>
                {
                    try
                    {
                        if (ChromiumPdf.IsEmbeddedResource(e.Request.Url))
                        {
                            await e.Request.ContinueAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            LogBlocked(logger, e.Request.Url);
                            await e.Request.AbortAsync().ConfigureAwait(false);
                        }
                    }
                    catch (PuppeteerException)
                    {
                        // La pagina pudo cerrarse mientras se resolvia la peticion
                    }
                };
            }

            await tab.SetContentAsync(html, new SetContentOptions { WaitUntil = [WaitUntilNavigation.Load], Timeout = timeoutMs }).ConfigureAwait(false);
            await tab.EvaluateFunctionAsync(ChromiumPdf.WaitForFonts).ConfigureAwait(false);

            var margin = ChromiumPdf.Margin(page);
            return await tab.PdfDataAsync(new PdfOptions
            {
                Format = page.Size switch
                {
                    PageSize.Letter => PaperFormat.Letter,
                    PageSize.Legal => PaperFormat.Legal,
                    _ => PaperFormat.A4,
                },
                Landscape = page.IsLandscape,
                PrintBackground = true,
                DisplayHeaderFooter = true,
                HeaderTemplate = ChromiumPdf.HeaderTemplate,
                FooterTemplate = ChromiumPdf.FooterTemplate,
                MarginOptions = new MarginOptions { Top = margin, Bottom = margin, Left = margin, Right = margin },
            }).ConfigureAwait(false);
        }
        finally
        {
            browsers.Slots.Release();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recurso externo bloqueado en la plantilla: {Url}")]
    private static partial void LogBlocked(ILogger logger, string url);
}
