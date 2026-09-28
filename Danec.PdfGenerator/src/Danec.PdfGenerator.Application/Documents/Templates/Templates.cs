namespace Danec.PdfGenerator.Application.Documents.Templates;

/// <summary>Caso de uso: plantillas disponibles y los motores que las aceptan.</summary>
public sealed class ListTemplatesHandler(ITemplateCatalog catalog)
{
    public IReadOnlyList<TemplateDescriptor> Handle() => catalog.List();
}

/// <summary>Caso de uso: datos de ejemplo de una plantilla.</summary>
public sealed class GetTemplateSampleHandler(ITemplateCatalog catalog)
{
    public async Task<Result<JsonElement>> HandleAsync(string name, CancellationToken cancellationToken)
    {
        var templateName = TemplateName.Create(name);
        if (templateName.IsFailure)
        {
            return templateName.Error;
        }

        var sample = await catalog.GetSampleDataAsync(templateName.Value, cancellationToken).ConfigureAwait(false);
        return sample is { } data ? data : DocumentErrors.SampleNotFound(templateName.Value.Value);
    }
}
