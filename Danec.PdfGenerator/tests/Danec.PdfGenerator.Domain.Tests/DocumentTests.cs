using Danec.PdfGenerator.Domain.Documents;

namespace Danec.PdfGenerator.Domain.Tests;

public class TemplateNameTests
{
    [Theory]
    [InlineData("factura", "factura")]
    [InlineData("  Factura-2026_v1 ", "factura-2026_v1")]
    public void Acepta_nombres_validos_y_normaliza(string input, string expected)
    {
        var result = TemplateName.Create(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("..\\secret")]
    [InlineData("html/factura")]
    [InlineData("C:factura")]
    [InlineData("-factura")]
    [InlineData("factura.html")]
    public void Rechaza_nombres_que_podrian_salir_de_la_carpeta(string input)
    {
        var result = TemplateName.Create(input);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Documents.TemplateNameInvalid");
    }

    [Fact]
    public void Rechaza_nombres_demasiado_largos() =>
        TemplateName.Create(new string('a', TemplateName.MaxLength + 1)).IsFailure.ShouldBeTrue();
}

public class TemplateSourceTests
{
    [Fact]
    public void Exige_template_o_html() =>
        TemplateSource.Create(null, " ").Error.ShouldBe(DocumentErrors.TemplateRequired);

    [Fact]
    public void No_admite_template_y_html_a_la_vez() =>
        TemplateSource.Create("factura", "<p>x</p>").Error.ShouldBe(DocumentErrors.TemplateAndHtml);

    [Theory]
    [InlineData(PdfEngine.QuestPdf, false)]
    [InlineData(PdfEngine.Overlay, false)]
    [InlineData(PdfEngine.HtmlRenderer, true)]
    [InlineData(PdfEngine.Playwright, true)]
    public void Html_en_linea_solo_para_motores_html(PdfEngine engine, bool supported)
    {
        var source = TemplateSource.Create(null, "<p>{{ x }}</p>").Value;

        source.EnsureSupportedBy(engine).IsSuccess.ShouldBe(supported);
    }
}

public class PageSettingsTests
{
    [Fact]
    public void Usa_A4_vertical_15mm_por_defecto()
    {
        var page = PageSettings.Create(null, null, null).Value;

        page.ShouldBe(PageSettings.Default);
        page.MarginPoints.ShouldBe(42.52, tolerance: 0.01);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    [InlineData(double.NaN)]
    public void Rechaza_margenes_fuera_de_rango(double margin) =>
        PageSettings.Create(PageSize.A4, PageOrientation.Portrait, margin).IsFailure.ShouldBeTrue();

    [Fact]
    public void Rechaza_valores_de_enum_no_definidos() =>
        PageSettings.Create((PageSize)99, null, null).Error.ShouldBe(DocumentErrors.InvalidPageSize);
}
