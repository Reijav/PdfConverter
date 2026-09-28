namespace Danec.PdfGenerator.Infrastructure.Engines.Docx;

/// <summary>
/// Motor "docx": MiniWord rellena la plantilla Word y Gotenberg (LibreOffice) la convierte a PDF.
/// El tamano de pagina y los margenes los define el propio .docx; de las opciones de pagina solo se aplica la orientacion.
/// </summary>
internal sealed class DocxPdfEngine(IWordDocumentGenerator word, GotenbergClient gotenberg) : IPdfEngine
{
    public PdfEngine Engine => PdfEngine.Docx;

    public async Task<Result<byte[]>> RenderAsync(PdfRenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Template.Name is not { } name)
        {
            return DocumentErrors.InlineHtmlNotSupported(Engine);
        }

        var docx = await word.GenerateAsync(name, request.Data, cancellationToken).ConfigureAwait(false);
        if (docx.IsFailure)
        {
            return docx.Error;
        }

        return await gotenberg
            .ConvertToPdfAsync(docx.Value, $"{name.Value}.docx", request.Page.IsLandscape, cancellationToken)
            .ConfigureAwait(false);
    }
}
