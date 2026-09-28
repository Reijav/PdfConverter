namespace Danec.PdfGenerator.Domain.Documents;

/// <summary>
/// Origen de la plantilla: una plantilla registrada (por nombre) o HTML Scriban enviado en la solicitud.
/// Exactamente uno de los dos.
/// </summary>
public sealed record TemplateSource
{
    public const int MaxInlineLength = 1_000_000;

    private TemplateSource(TemplateName? name, string? inlineHtml)
    {
        Name = name;
        InlineHtml = inlineHtml;
    }

    public TemplateName? Name { get; }

    public string? InlineHtml { get; }

    public bool IsInline => InlineHtml is not null;

    public string DisplayName => Name?.Value ?? "inline";

    public static Result<TemplateSource> Create(string? template, string? html)
    {
        var hasTemplate = !string.IsNullOrWhiteSpace(template);
        var hasHtml = !string.IsNullOrWhiteSpace(html);

        if (hasTemplate && hasHtml)
        {
            return DocumentErrors.TemplateAndHtml;
        }

        if (hasHtml)
        {
            return html!.Length > MaxInlineLength
                ? DocumentErrors.InlineHtmlTooLarge(MaxInlineLength)
                : new TemplateSource(null, html);
        }

        var name = TemplateName.Create(template);
        return name.IsFailure ? name.Error : new TemplateSource(name.Value, null);
    }

    /// <summary>El HTML en linea solo tiene sentido para motores que convierten HTML.</summary>
    public Result EnsureSupportedBy(PdfEngine engine) =>
        IsInline && !engine.UsesHtml()
            ? DocumentErrors.InlineHtmlNotSupported(engine)
            : Result.Success();
}
