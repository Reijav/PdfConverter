using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;

namespace Danec.PdfGenerator.Api.IntegrationTests;

public class WordEndpointsTests(ApiFactory factory)
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private static readonly Uri GotenbergHealth = new("http://localhost:3001/health");

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Word_reemplaza_marcadores_repite_filas_y_calcula_totales()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/word",
            new { template = "factura", data = await SampleAsync("factura"), fileName = "factura-001" },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Content.Headers.ContentType!.MediaType.ShouldBe(DocxContentType);
        response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("factura-001.docx");

        using var docx = WordprocessingDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync(Ct)), false);
        var text = docx.MainDocumentPart!.Document!.Body!.InnerText;

        text.ShouldContain("Empresa Demo S.A.");       // {{empresa_nombre}} (objeto anidado aplanado)
        text.ShouldContain("Manteca vegetal 1 kg");     // ultima fila de {{items.descripcion}}
        text.ShouldContain("56.40");                    // total de linea calculado (24 x 2.35)
        text.ShouldContain("283.64");                   // total calculado por FacturaWordEnricher
        text.ShouldNotContain("{{");
    }

    [Fact]
    public async Task Plantilla_word_inexistente_devuelve_404()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/word", new { template = "no-existe" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Documents.TemplateNotFound");
    }

    [Fact]
    public async Task El_catalogo_lista_la_plantilla_word()
    {
        var templates = await _client.GetFromJsonAsync<JsonArray>("/api/v1/pdf/templates", Ct);

        templates!.Select(t => $"{t!["name"]}:{t["kind"]}").ShouldContain("factura:Word");
    }

    [Fact]
    [Trait("Category", "Gotenberg")]
    public async Task Pdf_desde_word_con_gotenberg()
    {
        Assert.SkipUnless(await GotenbergDisponibleAsync(), "Gotenberg no esta corriendo en http://localhost:3001 (docker compose up gotenberg)");

        var response = await _client.PostAsJsonAsync(
            "/api/v1/pdf/docx",
            new { template = "factura", data = await SampleAsync("factura") },
            Ct);

        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(bytes));
        response.Headers.GetValues("X-Pdf-Engine").Single().ShouldBe("Docx");
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    private async Task<JsonNode> SampleAsync(string template) =>
        JsonNode.Parse(await _client.GetStringAsync($"/api/v1/pdf/templates/{template}/sample", Ct))!;

    private static async Task<bool> GotenbergDisponibleAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            return (await http.GetAsync(GotenbergHealth, Ct)).IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
