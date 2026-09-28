using Danec.PdfGenerator.Infrastructure.Engines.QuestPdf;

namespace Danec.PdfGenerator.Infrastructure.Engines.Docx;

/// <summary>
/// MiniWord solo reemplaza texto: no calcula ni formatea. Un "enricher" agrega a una plantilla Word concreta
/// los valores derivados (totales, numeros con 2 decimales). Se registra uno por plantilla que lo necesite.
/// </summary>
internal interface IWordTemplateEnricher
{
    /// <summary>Nombre de la plantilla (Templates/docx/{Name}.docx).</summary>
    string Name { get; }

    void Enrich(JsonElement data, Dictionary<string, object> model);
}

/// <summary>factura.docx: totales por linea, subtotal, IVA y total (misma regla que factura.html y QuestPDF).</summary>
internal sealed class FacturaWordEnricher : IWordTemplateEnricher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "factura";

    public void Enrich(JsonElement data, Dictionary<string, object> model)
    {
        if (data.ValueKind != JsonValueKind.Object || data.Deserialize<FacturaModel>(JsonOptions) is not { } factura)
        {
            return;
        }

        model["items"] = factura.Lineas
            .Select(i => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["codigo"] = i.Codigo ?? string.Empty,
                ["descripcion"] = i.Descripcion ?? string.Empty,
                ["cantidad"] = WordTemplateModel.Format(i.Cantidad, "0.##"),
                ["precio"] = WordTemplateModel.Format(i.Precio, "N2"),
                ["total"] = WordTemplateModel.Format(i.Total, "N2"),
            })
            .ToList();

        model["subtotal"] = WordTemplateModel.Format(factura.Subtotal, "N2");
        model["iva_porcentaje"] = WordTemplateModel.Format(factura.IvaPorcentaje ?? 15m, "0.##");
        model["iva"] = WordTemplateModel.Format(factura.Iva, "N2");
        model["total"] = WordTemplateModel.Format(factura.Total, "N2");
        model.TryAdd("observaciones", string.Empty);
    }
}
