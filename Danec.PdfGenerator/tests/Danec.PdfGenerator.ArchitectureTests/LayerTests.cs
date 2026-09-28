using System.Reflection;
using NetArchTest.Rules;

namespace Danec.PdfGenerator.ArchitectureTests;

public class LayerTests
{
    private const string Root = "Danec.PdfGenerator";

    private static readonly Assembly Domain = typeof(Danec.PdfGenerator.Domain.Abstractions.Result).Assembly;
    private static readonly Assembly Application = typeof(Danec.PdfGenerator.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Danec.PdfGenerator.Infrastructure.DependencyInjection).Assembly;

    /// <summary>Librerias de PDF / navegador: solo pueden vivir en Infrastructure.</summary>
    private static readonly string[] PdfLibraries =
        ["QuestPDF", "iTextSharp", "PdfSharp", "TheArtOfDev", "PuppeteerSharp", "Microsoft.Playwright", "Scriban", "MiniSoftware", "DocumentFormat.OpenXml"];

    [Fact]
    public void Domain_no_depende_de_otras_capas_ni_frameworks()
    {
        var result = Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny([$"{Root}.Application", $"{Root}.Infrastructure", $"{Root}.Api", "Microsoft.AspNetCore", .. PdfLibraries])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_no_conoce_Infrastructure_Api_ni_librerias_de_PDF()
    {
        var result = Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny([$"{Root}.Infrastructure", $"{Root}.Api", "Microsoft.AspNetCore", .. PdfLibraries])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_no_depende_de_Api()
    {
        var result = Types.InAssembly(Infrastructure).ShouldNot()
            .HaveDependencyOn($"{Root}.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Handlers_son_sealed()
    {
        var result = Types.InAssembly(Application).That()
            .HaveNameEndingWith("Handler").And().AreClasses()
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Cada_motor_del_enum_tiene_una_clase_interna_que_implementa_el_puerto()
    {
        // Reflexion (no NetArchTest): la interfaz puede venir heredada de HtmlPdfEngineBase
        var port = typeof(Danec.PdfGenerator.Application.Documents.IPdfEngine);
        var engines = Infrastructure.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && port.IsAssignableFrom(t))
            .ToList();

        engines.Count.ShouldBe(Enum.GetValues<Danec.PdfGenerator.Domain.Documents.PdfEngine>().Length);
        engines.Where(t => t.IsPublic).Select(t => t.Name).ShouldBeEmpty();
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Tipos que violan la regla: " + string.Join(", ", result.FailingTypeNames ?? []);
}
