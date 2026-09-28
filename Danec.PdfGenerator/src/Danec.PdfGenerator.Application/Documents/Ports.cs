namespace Danec.PdfGenerator.Application.Documents;

/// <summary>Todo lo que un motor necesita para generar un PDF.</summary>
/// <param name="Template">Plantilla registrada o HTML en linea.</param>
/// <param name="Data">Parametros (JSON libre) que se inyectan en la plantilla.</param>
/// <param name="Page">Tamano, orientacion y margenes.</param>
public sealed record PdfRenderRequest(TemplateSource Template, JsonElement Data, PageSettings Page);

/// <summary>Puerto de salida: cada motor (iText, QuestPDF, Chromium...) es un adaptador en Infrastructure.</summary>
public interface IPdfEngine
{
    PdfEngine Engine { get; }

    Task<Result<byte[]>> RenderAsync(PdfRenderRequest request, CancellationToken cancellationToken);
}

/// <summary>Devuelve el adaptador registrado para un motor (null si no hay ninguno).</summary>
public interface IPdfEngineFactory
{
    IPdfEngine? Get(PdfEngine engine);
}

/// <summary>Rellena una plantilla Word (.docx) con los parametros y devuelve el .docx resultante.</summary>
public interface IWordDocumentGenerator
{
    Task<Result<byte[]>> GenerateAsync(TemplateName template, JsonElement data, CancellationToken cancellationToken);
}

/// <summary>Convierte una plantilla HTML (Scriban) + parametros en HTML final.</summary>
public interface IHtmlTemplateRenderer
{
    /// <param name="template">Plantilla registrada o en linea.</param>
    /// <param name="data">Parametros de la solicitud.</param>
    /// <param name="engine">
    /// Motor que convertira el HTML (null en la vista previa). Se expone a la plantilla como
    /// <c>pdf.motor</c> y <c>pdf.css3</c> para adaptar el diseno a lo que soporta cada motor.
    /// </param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    Task<Result<string>> RenderAsync(TemplateSource template, JsonElement data, PdfEngine? engine, CancellationToken cancellationToken);
}

public sealed record TemplateDescriptor(string Name, TemplateKind Kind, IReadOnlyList<PdfEngine> Engines, bool HasSampleData);

public interface ITemplateCatalog
{
    IReadOnlyList<TemplateDescriptor> List();

    Task<JsonElement?> GetSampleDataAsync(TemplateName name, CancellationToken cancellationToken);
}
