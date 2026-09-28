using System.Net.Http.Headers;

namespace Danec.PdfGenerator.Infrastructure.Engines.Docx;

/// <summary>
/// Cliente HTTP de Gotenberg: POST /forms/libreoffice/convert (multipart, campo "files") devuelve el PDF.
/// Usa IHttpClientFactory con un cliente con nombre para poder ser singleton sin retener conexiones viejas.
/// </summary>
internal sealed partial class GotenbergClient(IHttpClientFactory httpClientFactory, ILogger<GotenbergClient> logger)
{
    public const string HttpClientName = "gotenberg";
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public async Task<Result<byte[]>> ConvertToPdfAsync(byte[] document, string fileName, bool landscape, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(document);
        file.Headers.ContentType = new MediaTypeHeaderValue(DocxContentType);
        form.Add(file, "files", fileName);
        if (landscape)
        {
            form.Add(new StringContent("true"), "landscape");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var response = await client
                .PostAsync(new Uri("forms/libreoffice/convert", UriKind.Relative), form, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return DocumentErrors.RenderFailed(PdfEngine.Docx, $"Gotenberg respondio {(int)response.StatusCode}: {Truncate(body)}");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogUnavailable(logger, client.BaseAddress, ex.Message);
            return DocumentErrors.ConverterUnavailable("Gotenberg", $"{client.BaseAddress} ({ex.Message})");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DocumentErrors.ConverterUnavailable("Gotenberg", $"tiempo de espera agotado ({client.Timeout.TotalSeconds:0} s)");
        }
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300] + "...";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gotenberg no disponible en {BaseAddress}: {Reason}")]
    private static partial void LogUnavailable(ILogger logger, Uri? baseAddress, string reason);
}
