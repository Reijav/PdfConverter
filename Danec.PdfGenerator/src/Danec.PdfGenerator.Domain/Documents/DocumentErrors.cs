using System.Globalization;

namespace Danec.PdfGenerator.Domain.Documents;

public static class DocumentErrors
{
    public static readonly Error TemplateRequired = Error.Validation(
        "Documents.TemplateRequired", "Debe indicar 'template' (plantilla registrada) o 'html' (plantilla en linea).");

    public static readonly Error TemplateAndHtml = Error.Validation(
        "Documents.TemplateAndHtml", "Envie 'template' o 'html', no ambos.");

    public static readonly Error InvalidPageSize = Error.Validation(
        "Documents.InvalidPageSize", "Tamano de pagina no soportado. Valores: A4, Letter, Legal.");

    public static readonly Error InvalidOrientation = Error.Validation(
        "Documents.InvalidOrientation", "Orientacion no soportada. Valores: Portrait, Landscape.");

    public static Error TemplateNameInvalid(string value) => Error.Validation(
        "Documents.TemplateNameInvalid",
        $"El nombre de plantilla '{value}' no es valido: use letras, numeros, '-' o '_' (maximo {TemplateName.MaxLength}).");

    public static Error InlineHtmlTooLarge(int max) => Error.Validation(
        "Documents.InlineHtmlTooLarge", $"El HTML en linea supera el maximo de {max.ToString("N0", CultureInfo.InvariantCulture)} caracteres.");

    public static Error InlineHtmlNotSupported(PdfEngine engine) => Error.Validation(
        "Documents.InlineHtmlNotSupported", $"El motor {engine} no admite HTML en linea; use una plantilla registrada.");

    public static Error InvalidMargin(double margin) => Error.Validation(
        "Documents.InvalidMargin",
        $"El margen {margin.ToString(CultureInfo.InvariantCulture)} mm no es valido (0 a {PageSettings.MaxMarginMm.ToString(CultureInfo.InvariantCulture)} mm).");

    public static Error TemplateNotFound(string name, TemplateKind kind) => Error.NotFound(
        "Documents.TemplateNotFound", $"No existe la plantilla {kind} '{name}'.");

    public static Error SampleNotFound(string name) => Error.NotFound(
        "Documents.SampleNotFound", $"La plantilla '{name}' no tiene datos de ejemplo.");

    public static Error TemplateSyntax(string detail) => Error.Validation(
        "Documents.TemplateSyntax", $"Error en la plantilla: {detail}");

    public static Error InvalidData(string detail) => Error.Validation(
        "Documents.InvalidData", $"Los datos enviados no son validos para la plantilla: {detail}");

    public static Error EngineUnavailable(PdfEngine engine) => Error.Failure(
        "Documents.EngineUnavailable", $"El motor {engine} no esta registrado.");

    public static Error ConverterUnavailable(string converter, string detail) => Error.Failure(
        "Documents.ConverterUnavailable", $"El servicio de conversion {converter} no esta disponible: {detail}");

    public static Error WordGenerationFailed(string detail) => Error.Failure(
        "Documents.WordGenerationFailed", $"No se pudo generar el documento Word: {detail}");

    public static Error RenderFailed(PdfEngine engine, string detail) => Error.Failure(
        "Documents.RenderFailed", $"El motor {engine} no pudo generar el PDF: {detail}");
}
