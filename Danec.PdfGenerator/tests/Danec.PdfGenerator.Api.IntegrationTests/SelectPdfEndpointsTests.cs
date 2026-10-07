using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Danec.PdfGenerator.Api.IntegrationTests;

public class SelectPdfEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Convierte de verdad: necesita los paquetes de SelectPdf restaurados (sin licencia sale con marca de agua).</summary>
    [Theory]
    [Trait("Category", "SelectPdf")]
    [InlineData("/api/v1/selectpdf/html-pdf", "SelectPdf")]
    [InlineData("/api/v1/selectpdf/word-pdf", "SelectPdfWord")]
    public async Task Genera_la_factura_sin_fondo(string url, string engine)
    {
        var response = await _client.PostAsJsonAsync(
            url,
            new { template = "factura", data = await SampleAsync("factura"), fileName = "factura-selectpdf" },
            Ct);

        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(bytes));
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("factura-selectpdf.pdf");
        response.Headers.GetValues("X-Pdf-Engine").Single().ShouldBe(engine);
        response.Headers.Contains("X-Background").ShouldBeFalse();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    [Trait("Category", "SelectPdf")]
    public async Task Html_en_linea_se_muestra_en_el_navegador()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/selectpdf/html-pdf?inline=true",
            new { html = "<h1>Hola {{ nombre }}</h1>", data = new { nombre = "Javier" } },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
    }

    [Theory]
    [InlineData("/api/v1/selectpdf/html-pdf")]
    [InlineData("/api/v1/selectpdf/word-pdf")]
    public async Task Plantilla_inexistente_devuelve_404(string url)
    {
        var response = await _client.PostAsJsonAsync(url, new { template = "no-existe" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Documents.TemplateNotFound");
    }

    [Theory]
    [InlineData("/api/v1/selectpdf/html-pdf")]
    [InlineData("/api/v1/selectpdf/word-pdf")]
    public async Task Nombre_de_plantilla_invalido_devuelve_400(string url)
    {
        var response = await _client.PostAsJsonAsync(url, new { template = "../appsettings" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task El_catalogo_ofrece_SelectPdf_para_html_y_word()
    {
        var templates = await _client.GetFromJsonAsync<JsonArray>("/api/v1/pdf/templates", Ct);

        Engines(templates!, "factura:Html").ShouldContain("SelectPdf");
        Engines(templates!, "factura:Word").ShouldContain("SelectPdfWord");
    }

    private static List<string> Engines(JsonArray templates, string key) =>
        templates.Single(t => $"{t!["name"]}:{t["kind"]}" == key)!["engines"]!.AsArray().Select(e => e!.ToString()).ToList();

    private async Task<JsonNode> SampleAsync(string template) =>
        JsonNode.Parse(await _client.GetStringAsync($"/api/v1/pdf/templates/{template}/sample", Ct))!;
}
