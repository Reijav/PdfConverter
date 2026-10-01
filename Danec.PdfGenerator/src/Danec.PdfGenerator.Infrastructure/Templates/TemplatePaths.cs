namespace Danec.PdfGenerator.Infrastructure.Templates;

/// <summary>
/// Resuelve rutas de plantillas. <see cref="TemplateName"/> ya garantiza que el nombre no contiene
/// separadores ni "..", por lo que ninguna ruta puede salir de <see cref="Root"/>.
/// </summary>
internal sealed class TemplatePaths
{
    public TemplatePaths(IOptions<PdfGeneratorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configured = options.Value.TemplatesPath;
        Root = Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured));
    }

    public string Root { get; }

    public string HtmlDirectory => Path.Combine(Root, "html");

    public string OverlayDirectory => Path.Combine(Root, "overlay");

    public string SamplesDirectory => Path.Combine(Root, "samples");

    public string DocxDirectory => Path.Combine(Root, "docx");

    public string BackgroundDirectory => Path.Combine(Root, "fondo");

    public string HtmlFile(TemplateName name) => Path.Combine(HtmlDirectory, $"{name.Value}.html");

    public string OverlayPdf(TemplateName name) => Path.Combine(OverlayDirectory, $"{name.Value}.pdf");

    public string OverlayMap(TemplateName name) => Path.Combine(OverlayDirectory, $"{name.Value}.json");

    public string DocxFile(TemplateName name) => Path.Combine(DocxDirectory, $"{name.Value}.docx");

    public string SampleFile(TemplateName name) => Path.Combine(SamplesDirectory, $"{name.Value}.json");
}
