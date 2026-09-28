using Danec.PdfGenerator.Infrastructure.Engines.QuestPdf;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Danec.PdfGenerator.Infrastructure.Templates;

internal sealed class FileSystemTemplateCatalog(TemplatePaths paths, IEnumerable<IQuestPdfTemplate> codeTemplates) : ITemplateCatalog
{
    public IReadOnlyList<TemplateDescriptor> List()
    {
        var html = Names(paths.HtmlDirectory, "*.html")
            .Select(name => Describe(name, TemplateKind.Html));

        var overlay = Names(paths.OverlayDirectory, "*.pdf")
            .Where(name => File.Exists(Path.Combine(paths.OverlayDirectory, $"{name}.json")))
            .Select(name => Describe(name, TemplateKind.Overlay));

        var code = codeTemplates.Select(t => Describe(t.Name, TemplateKind.Code));

        var word = Names(paths.DocxDirectory, "*.docx")
            .Where(name => !name.StartsWith('~'))   // archivos temporales de Word abierto
            .Select(name => Describe(name, TemplateKind.Word));

        return [.. html.Concat(overlay).Concat(code).Concat(word).OrderBy(t => t.Name, StringComparer.Ordinal).ThenBy(t => t.Kind)];
    }

    public async Task<JsonElement?> GetSampleDataAsync(TemplateName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        var file = paths.SampleFile(name);
        if (!File.Exists(file))
        {
            return null;
        }

        var stream = File.OpenRead(file);
        await using (stream.ConfigureAwait(false))
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return document.RootElement.Clone();
        }
    }

    private static IEnumerable<string> Names(string directory, string pattern) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, pattern).Select(Path.GetFileNameWithoutExtension).OfType<string>()
            : [];

    private TemplateDescriptor Describe(string name, TemplateKind kind) =>
        new(name, kind, kind.GetEngines(), File.Exists(Path.Combine(paths.SamplesDirectory, $"{name}.json")));
}

internal sealed class TemplatesHealthCheck(TemplatePaths paths) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(Directory.Exists(paths.Root)
            ? HealthCheckResult.Healthy($"Plantillas en {paths.Root}")
            : HealthCheckResult.Unhealthy($"No existe la carpeta de plantillas {paths.Root}"));
}
