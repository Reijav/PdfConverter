using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;

namespace Danec.PdfGenerator.Api.IntegrationTests;

public class MiniPdfEndpointsTests(ApiFactory factory)
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Docx_reemplaza_marcadores_y_calcula_totales()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/minipdf/docx",
            new { template = "factura", data = await SampleAsync("factura"), fileName = "factura-minipdf" },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Content.Headers.ContentType!.MediaType.ShouldBe(DocxContentType);
        response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("factura-minipdf.docx");

        using var docx = WordprocessingDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync(Ct)), false);
        var text = docx.MainDocumentPart!.Document!.Body!.InnerText;
        text.ShouldContain("Empresa Demo S.A.");
        text.ShouldContain("283.64");
    }

    [Fact]
    public async Task Pdf_se_genera_en_proceso_sin_gotenberg()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/minipdf/pdf",
            new { template = "factura", data = await SampleAsync("factura"), fileName = "factura-minipdf" },
            Ct);

        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(bytes));
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("factura-minipdf.pdf");
        response.Headers.GetValues("X-Pdf-Engine").Single().ShouldBe("MiniPdf");
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task Pdf_inline_se_muestra_en_el_navegador()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/minipdf/pdf?inline=true",
            new { template = "factura", data = await SampleAsync("factura") },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
    }

    [Theory]
    [InlineData("/api/v1/minipdf/docx")]
    [InlineData("/api/v1/minipdf/pdf")]
    public async Task Plantilla_inexistente_devuelve_404(string url)
    {
        var response = await _client.PostAsJsonAsync(url, new { template = "no-existe" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Documents.TemplateNotFound");
    }

    [Theory]
    [InlineData("/api/v1/minipdf/docx")]
    [InlineData("/api/v1/minipdf/pdf")]
    public async Task Nombre_de_plantilla_invalido_devuelve_400(string url)
    {
        var response = await _client.PostAsJsonAsync(url, new { template = "../appsettings" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task El_catalogo_ofrece_MiniPdf_para_las_plantillas_word()
    {
        var templates = await _client.GetFromJsonAsync<JsonArray>("/api/v1/pdf/templates", Ct);

        var factura = templates!.Single(t => $"{t!["name"]}:{t["kind"]}" == "factura:Word")!;
        factura["engines"]!.AsArray().Select(e => e!.ToString()).ShouldContain("MiniPdf");
    }

    [Fact]
    public async Task Word_pdf_fondo_convierte_el_docx_a_pdf_antes_de_estampar_el_fondo()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/minipdf/word-pdf-fondo",
            new { template = "factura", data = await SampleAsync("factura"), fileName = "factura-fondo", fondo = new { imagen = "fondo" } },
            Ct);

        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(bytes));
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("factura-fondo.pdf");
        response.Headers.GetValues("X-Pdf-Engine").Single().ShouldBe("MiniPdf");
        response.Headers.GetValues("X-Background").Single().ShouldBe("fondo.jpg");
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task Word_pdf_fondo_con_fondo_inexistente_devuelve_404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/minipdf/word-pdf-fondo",
            new { template = "factura", fondo = new { imagen = "no-existe" } },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Documents.BackgroundNotFound");
    }

    private async Task<JsonNode> SampleAsync(string template) =>
        JsonNode.Parse(await _client.GetStringAsync($"/api/v1/pdf/templates/{template}/sample", Ct))!;
}
