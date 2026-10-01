using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace Danec.PdfGenerator.Api.IntegrationTests;

public class PdfFondoEndpointsTests(ApiFactory factory)
{
    private const string Url = "/api/v1/pdf/htmlrenderer-fondo";

    /// <summary>HTML en linea que ocupa varias paginas.</summary>
    private const string MultiPageHtml = "<html><body>{{ for i in 1..160 }}<p>Linea {{ i }}</p>{{ end }}</body></html>";

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sin_fondo_explicito_usa_la_imagen_por_defecto_en_todas_las_paginas()
    {
        var response = await _client.PostAsJsonAsync(
            Url,
            new { template = "factura", data = await SampleAsync("factura"), fileName = "factura-fondo" },
            Ct);

        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(bytes));
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("factura-fondo.pdf");
        response.Headers.GetValues("X-Pdf-Engine").Single().ShouldBe("HtmlRenderer");
        response.Headers.GetValues("X-Background").Single().ShouldBe("fondo.jpg");
        response.Headers.Contains("X-Background-Ms").ShouldBeTrue();

        using var pdf = Open(bytes);
        pdf.Pages.Cast<PdfPage>().ShouldAllBe(p => HasImage(p));
    }

    [Fact]
    public async Task Inline_se_muestra_en_el_navegador()
    {
        var response = await _client.PostAsJsonAsync(
            Url + "?inline=true",
            new { template = "factura", data = await SampleAsync("factura") },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
    }

    [Theory]
    [InlineData("Todas", true)]
    [InlineData("Primera", false)]
    public async Task Paginas_controla_que_paginas_reciben_el_fondo(string paginas, bool segundaConFondo)
    {
        var response = await _client.PostAsJsonAsync(
            Url,
            new { html = MultiPageHtml, fondo = new { paginas, ajuste = "Pagina" } },
            Ct);

        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(bytes));

        using var pdf = Open(bytes);
        pdf.PageCount.ShouldBeGreaterThan(1);
        HasImage(pdf.Pages[0]).ShouldBeTrue();
        HasImage(pdf.Pages[1]).ShouldBe(segundaConFondo);
    }

    [Theory]
    [InlineData("Ancho")]
    [InlineData("Pagina")]
    [InlineData("Centrado")]
    public async Task Todos_los_ajustes_generan_el_pdf(string ajuste)
    {
        var response = await _client.PostAsJsonAsync(
            Url,
            new { template = "factura", data = await SampleAsync("factura"), fondo = new { imagen = "fondo", ajuste } },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Fondo_inexistente_devuelve_404()
    {
        var response = await _client.PostAsJsonAsync(
            Url,
            new { template = "factura", data = await SampleAsync("factura"), fondo = new { imagen = "no-existe" } },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Documents.BackgroundNotFound");
    }

    [Theory]
    [InlineData("../appsettings")]
    [InlineData("fondo de cartas")]
    [InlineData("fondo-de-cartas.png")]
    public async Task Nombre_de_fondo_invalido_devuelve_400(string imagen)
    {
        var response = await _client.PostAsJsonAsync(
            Url,
            new { template = "factura", fondo = new { imagen } },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Documents.BackgroundNameInvalid");
    }

    [Fact]
    public async Task Ajuste_desconocido_devuelve_400()
    {
        var response = await _client.PostAsJsonAsync(
            Url,
            new { template = "factura", fondo = new { ajuste = "Mosaico" } },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Plantilla_inexistente_devuelve_404()
    {
        var response = await _client.PostAsJsonAsync(Url, new { template = "no-existe" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Documents.TemplateNotFound");
    }

    private static PdfDocument Open(byte[] bytes) => PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);

    /// <summary>true si la pagina referencia al menos una imagen (XObject /Subtype /Image).</summary>
    private static bool HasImage(PdfPage page)
    {
        var xObjects = page.Resources.Elements.GetDictionary("/XObject");
        if (xObjects is null)
        {
            return false;
        }

        return xObjects.Elements.Values
            .Select(v => v is PdfReference r ? r.Value : v)
            .OfType<PdfDictionary>()
            .Any(d => d.Elements.GetName("/Subtype") == "/Image");
    }

    private async Task<JsonNode> SampleAsync(string template) =>
        JsonNode.Parse(await _client.GetStringAsync($"/api/v1/pdf/templates/{template}/sample", Ct))!;
}
