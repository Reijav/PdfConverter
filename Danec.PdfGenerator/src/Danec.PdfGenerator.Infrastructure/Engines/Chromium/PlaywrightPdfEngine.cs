using Microsoft.Playwright;

namespace Danec.PdfGenerator.Infrastructure.Engines.Chromium;

/// <summary>Un solo Chromium de Playwright por proceso; se relanza si se cae.</summary>
internal sealed partial class PlaywrightBrowserProvider(IOptions<PdfGeneratorOptions> options, ILogger<PlaywrightBrowserProvider> logger)
    : IAsyncDisposable
{
    private readonly SemaphoreSlim _launchLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public SemaphoreSlim Slots { get; } = new(options.Value.Chromium.MaxConcurrency, options.Value.Chromium.MaxConcurrency);

    public async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true } ready)
        {
            return ready;
        }

        await _launchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_browser is { IsConnected: true } current)
            {
                return current;
            }

            if (_browser is not null)
            {
                await _browser.DisposeAsync().ConfigureAwait(false);
            }

            var settings = options.Value.Chromium;
            _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                ExecutablePath = settings.ExecutablePath,
                Args = settings.Args,
                Timeout = settings.TimeoutSeconds * 1000,
            }).ConfigureAwait(false);

            LogLaunched(logger, _browser.Version);
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

        _playwright?.Dispose();
        _launchLock.Dispose();
        Slots.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Playwright: Chromium {Version} iniciado")]
    private static partial void LogLaunched(ILogger logger, string version);
}

/// <summary>Microsoft.Playwright (Apache 2.0): Chromium headless, HTML5/CSS3 completo, encabezado y pie con numeracion.</summary>
internal sealed partial class PlaywrightPdfEngine(
    IHtmlTemplateRenderer renderer,
    PlaywrightBrowserProvider browsers,
    IOptions<PdfGeneratorOptions> options,
    ILogger<PlaywrightPdfEngine> logger) : HtmlPdfEngineBase(renderer, logger)
{
    public override PdfEngine Engine => PdfEngine.Playwright;

    protected override async Task<byte[]> ConvertAsync(string html, PageSettings page, CancellationToken cancellationToken)
    {
        var settings = options.Value.Chromium;
        var timeoutMs = settings.TimeoutSeconds * 1000;

        await browsers.Slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var browser = await browsers.GetBrowserAsync(cancellationToken).ConfigureAwait(false);
            await using var context = await browser.NewContextAsync().ConfigureAwait(false);
            var tab = await context.NewPageAsync().ConfigureAwait(false);
            tab.SetDefaultTimeout(timeoutMs);

            if (!settings.AllowExternalResources)
            {
                await tab.RouteAsync("**/*", route =>
                {
                    if (ChromiumPdf.IsEmbeddedResource(route.Request.Url))
                    {
                        return route.ContinueAsync();
                    }

                    LogBlocked(logger, route.Request.Url);
                    return route.AbortAsync();
                }).ConfigureAwait(false);
            }

            await tab.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load, Timeout = timeoutMs }).ConfigureAwait(false);
            await tab.EvaluateAsync(ChromiumPdf.WaitForFonts).ConfigureAwait(false);

            var margin = ChromiumPdf.Margin(page);
            return await tab.PdfAsync(new PagePdfOptions
            {
                Format = page.Size switch
                {
                    PageSize.Letter => "Letter",
                    PageSize.Legal => "Legal",
                    _ => "A4",
                },
                Landscape = page.IsLandscape,
                PrintBackground = true,
                DisplayHeaderFooter = true,
                HeaderTemplate = ChromiumPdf.HeaderTemplate,
                FooterTemplate = ChromiumPdf.FooterTemplate,
                Margin = new Margin { Top = margin, Bottom = margin, Left = margin, Right = margin },
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
