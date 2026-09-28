using System.Text.RegularExpressions;

namespace Danec.PdfGenerator.Domain.Documents;

/// <summary>
/// Nombre de plantilla. Solo admite letras, numeros, '-' y '_', por lo que nunca puede
/// usarse para salir de la carpeta de plantillas (path traversal).
/// </summary>
public sealed partial record TemplateName
{
    public const int MaxLength = 64;

    private TemplateName(string value) => Value = value;

    public string Value { get; }

    public static Result<TemplateName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DocumentErrors.TemplateRequired;
        }

#pragma warning disable CA1308 // Los nombres de archivo de plantilla se normalizan a minusculas por convencion
        var normalized = value.Trim().ToLowerInvariant();
#pragma warning restore CA1308
        if (normalized.Length > MaxLength || !Pattern().IsMatch(normalized))
        {
            return DocumentErrors.TemplateNameInvalid(value);
        }

        return new TemplateName(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
