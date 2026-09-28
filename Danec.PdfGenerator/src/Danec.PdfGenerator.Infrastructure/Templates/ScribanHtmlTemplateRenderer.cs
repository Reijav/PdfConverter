using System.Collections.Concurrent;
using Scriban;
using Scriban.Syntax;

namespace Danec.PdfGenerator.Infrastructure.Templates;

/// <summary>
/// Motor de plantillas Scriban (BSD-2). Las plantillas en disco se parsean una vez y se cachean
/// hasta que cambia el archivo, asi que puedes editar el HTML con la API corriendo.
/// </summary>
internal sealed class ScribanHtmlTemplateRenderer(TemplatePaths paths) : IHtmlTemplateRenderer
{
    private readonly ConcurrentDictionary<string, CachedTemplate> _cache = new(StringComparer.Ordinal);

    public async Task<Result<string>> RenderAsync(TemplateSource template, JsonElement data, PdfEngine? engine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(template);

        var parsed = template.IsInline
            ? Template.Parse(template.InlineHtml!)
            : await LoadAsync(template.Name!, cancellationToken).ConfigureAwait(false);

        if (parsed is null)
        {
            return DocumentErrors.TemplateNotFound(template.DisplayName, TemplateKind.Html);
        }

        if (parsed.HasErrors)
        {
            return DocumentErrors.TemplateSyntax(string.Join("; ", parsed.Messages.Select(m => m.ToString())));
        }

        var context = new TemplateContext
        {
            LoopLimit = 10_000,
            RecursiveLimit = 50,
            StrictVariables = false,
        };
        context.PushGlobal(ScribanJson.ToGlobals(data, engine));

        try
        {
            return await parsed.RenderAsync(context).ConfigureAwait(false);
        }
        catch (ScriptRuntimeException ex)
        {
            return DocumentErrors.TemplateSyntax(ex.Message);
        }
    }

    private async Task<Template?> LoadAsync(TemplateName name, CancellationToken cancellationToken)
    {
        var file = paths.HtmlFile(name);
        if (!File.Exists(file))
        {
            return null;
        }

        var stamp = File.GetLastWriteTimeUtc(file);
        if (_cache.TryGetValue(name.Value, out var cached) && cached.Stamp == stamp)
        {
            return cached.Template;
        }

        var text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
        // Solo el nombre del archivo: los mensajes de error no deben exponer rutas del servidor
        var parsed = Template.Parse(text, Path.GetFileName(file));
        _cache[name.Value] = new CachedTemplate(stamp, parsed);
        return parsed;
    }

    private sealed record CachedTemplate(DateTime Stamp, Template Template);
}
