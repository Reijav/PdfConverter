using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Danec.PdfGenerator.Api.IntegrationTests;

public class PdfEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("itext")]
    [InlineData("questpdf")]
    [InlineData("htmlrenderer")]
    public async Task Motores_dotnet_generan_la_factura(string engine) =>
        await AssertPdfAsync(engine, "factura");

    [Theory]
    [Trait("Category", "Chromium")]
    [InlineData("puppeteer")]
    [InlineData("playwright")]
    public async Task Motores_chromium_generan_la_factura(string engine) =>
        await AssertPdfAsync(engine, "factura");

    [Fact]
    public async Task Overlay_genera_el_certificado() =>
        await AssertPdfAsync("overlay", "certificado", new JsonObject { ["orientation"] = "Landscape" });

    /// <summary>
    /// Regresion: HtmlRenderer registraba su FontResolver al primer uso y PDFsharp lo prohibe si otro
    /// motor ya uso fuentes (TypeInitializationException). Ver PdfSharpFontSetup.
    /// </summary>
    [Fact]
    public async Task HtmlRenderer_funciona_aunque_Overlay_use_PDFsharp_primero()
    {
        await AssertPdfAsync("overlay", "certificado", new JsonObject { ["orientation"] = "Landscape" });
        await AssertPdfAsync("htmlrenderer", "factura");
    }

    [Fact]
    public async Task Lista_las_plantillas_con_sus_motores()
    {
        var templates = await _client.GetFromJsonAsync<JsonArray>("/api/v1/pdf/templates", Ct);

        var names = templates!.Select(t => $"{t!["name"]}:{t["kind"]}").ToList();
        names.ShouldContain("factura:Html");
        names.ShouldContain("factura:Code");
        names.ShouldContain("certificado:Overlay");
    }

    [Theory]
    [InlineData("../appsettings", HttpStatusCode.BadRequest)]
    [InlineData("no-existe", HttpStatusCode.NotFound)]
    public async Task Rechaza_plantillas_invalidas_o_inexistentes(string template, HttpStatusCode expected)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/pdf/htmlrenderer", new { template }, Ct);

        response.StatusCode.ShouldBe(expected);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task QuestPdf_no_acepta_html_en_linea()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/pdf/questpdf", new { html = "<p>hola</p>" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Documents.InlineHtmlNotSupported");
    }

    [Fact]
    public async Task Overlay_exige_los_campos_obligatorios()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/pdf/overlay", new { template = "certificado", data = new { nombre = "Ana" } }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("curso");
    }

    [Fact]
    public async Task Los_parametros_se_escapan_como_html()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/pdf/preview-html",
            new { html = "<p>{{ nombre }}</p>", data = new { nombre = "<script>alert(1)</script>" } },
            Ct);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(Ct);
        html.ShouldBe("<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>");
    }

    [Theory]
    [InlineData("Playwright", "class=\"marca-agua\"")]
    [InlineData("Puppeteer", "class=\"marca-agua\"")]
    [InlineData("HtmlRenderer", "class=\"marca-agua-simple\"")]
    [InlineData("IText", "class=\"marca-agua-simple\"")]
    public async Task Marca_de_agua_se_adapta_al_motor(string engine, string expectedMarkup)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/pdf/preview-html",
            new { template = "factura", engine, data = await SampleWithAsync("factura", "marcaAgua", "COPIA") },
            Ct);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(Ct);
        html.ShouldContain(expectedMarkup);
        html.ShouldContain("COPIA");
    }

    [Fact]
    public async Task Sin_marcaAgua_no_se_dibuja_marca()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/pdf/preview-html",
            new { template = "factura", engine = "Playwright", data = await SampleWithAsync("factura", "marcaAgua", null) },
            Ct);

        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("<div class=\"marca-agua");
    }

    [Fact]
    public async Task Variable_pdf_expone_el_motor()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/pdf/preview-html",
            new { html = "{{ pdf.motor }}|{{ pdf.css3 }}", engine = "HtmlRenderer" },
            Ct);

        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("HtmlRenderer|false");
    }

    [Fact]
    public async Task Html_en_linea_con_parametros_genera_pdf()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/pdf/htmlrenderer",
            new { html = "<h1>Hola {{ nombre }}</h1><p>Total: {{ total | math.format \"N2\" }}</p>", data = new { nombre = "Javier", total = 1234.5 }, fileName = "saludo" },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("saludo.pdf");
    }

    [Fact]
    public async Task Error_de_sintaxis_scriban_devuelve_400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/pdf/htmlrenderer", new { html = "<p>{{ for x in }}</p>" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Documents.TemplateSyntax");
    }

    private async Task<JsonNode> SampleWithAsync(string template, string key, string? value)
    {
        var sample = JsonNode.Parse(await _client.GetStringAsync($"/api/v1/pdf/templates/{template}/sample", Ct))!;
        sample[key] = value;
        return sample;
    }

    private async Task AssertPdfAsync(string engine, string template, JsonObject? page = null)
    {
        var sample = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/pdf/templates/{template}/sample", Ct);
        var body = new JsonObject
        {
            ["template"] = template,
            ["data"] = JsonNode.Parse(sample.GetRawText()),
            ["page"] = page,
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/pdf/{engine}", body, Ct);

        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(bytes));
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Headers.GetValues("X-Pdf-Engine").Single().ShouldBe(engine, StringCompareShould.IgnoreCase);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }
}
