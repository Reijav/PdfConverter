using Microsoft.Extensions.DependencyInjection;

namespace Danec.PdfGenerator.Infrastructure.Engines;

/// <summary>
/// Cada motor se registra como keyed service con su <see cref="PdfEngine"/> como clave
/// (ver DependencyInjection.AddPdfEngines). La fabrica solo traduce la clave al adaptador.
/// </summary>
internal sealed class PdfEngineFactory(IServiceProvider services) : IPdfEngineFactory
{
    public IPdfEngine? Get(PdfEngine engine) => services.GetKeyedService<IPdfEngine>(engine);
}
