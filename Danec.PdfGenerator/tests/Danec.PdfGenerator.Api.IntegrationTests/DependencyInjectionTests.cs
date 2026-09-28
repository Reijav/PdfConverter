using Danec.PdfGenerator.Application.Documents;
using Danec.PdfGenerator.Application.Documents.GeneratePdf;
using Danec.PdfGenerator.Application.Documents.PreviewHtml;
using Danec.PdfGenerator.Application.Documents.Templates;
using Danec.PdfGenerator.Domain.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace Danec.PdfGenerator.Api.IntegrationTests;

public class DependencyInjectionTests(ApiFactory factory)
{
    public static TheoryData<PdfEngine> AllEngines => [.. Enum.GetValues<PdfEngine>()];

    [Theory]
    [MemberData(nameof(AllEngines))]
    public void Cada_motor_tiene_su_adaptador_registrado_con_la_clave_correcta(PdfEngine engine)
    {
        var engines = factory.Services.GetRequiredService<IPdfEngineFactory>();

        var adapter = engines.Get(engine);

        adapter.ShouldNotBeNull($"Falta services.AddKeyedSingleton<IPdfEngine, ...>(PdfEngine.{engine})");
        adapter.Engine.ShouldBe(engine);
    }

    [Theory]
    [InlineData(typeof(GeneratePdfHandler))]
    [InlineData(typeof(PreviewHtmlHandler))]
    [InlineData(typeof(ListTemplatesHandler))]
    [InlineData(typeof(GetTemplateSampleHandler))]
    public void Cada_caso_de_uso_se_puede_resolver(Type handler)
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetService(handler).ShouldNotBeNull();
    }
}
