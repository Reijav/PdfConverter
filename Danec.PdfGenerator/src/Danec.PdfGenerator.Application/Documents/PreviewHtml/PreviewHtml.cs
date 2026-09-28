namespace Danec.PdfGenerator.Application.Documents.PreviewHtml;

/// <summary>
/// Solicitud de vista previa. <paramref name="Engine"/> simula el motor destino
/// (variables <c>pdf.motor</c> / <c>pdf.css3</c> de la plantilla).
/// </summary>
public sealed record PreviewHtmlCommand(string? Template, string? Html, JsonElement Data, PdfEngine? Engine = null);

/// <summary>Caso de uso: devuelve el HTML ya combinado con los parametros, sin generar el PDF.</summary>
public sealed class PreviewHtmlHandler(IHtmlTemplateRenderer renderer)
{
    public async Task<Result<string>> HandleAsync(PreviewHtmlCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var source = TemplateSource.Create(command.Template, command.Html);
        if (source.IsFailure)
        {
            return source.Error;
        }

        return await renderer.RenderAsync(source.Value, command.Data, command.Engine, cancellationToken).ConfigureAwait(false);
    }
}
