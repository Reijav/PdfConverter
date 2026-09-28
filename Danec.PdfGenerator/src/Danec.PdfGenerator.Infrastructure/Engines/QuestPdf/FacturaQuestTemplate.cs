using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DocPageSize = Danec.PdfGenerator.Domain.Documents.PageSize;

namespace Danec.PdfGenerator.Infrastructure.Engines.QuestPdf;

/// <summary>Plantilla "factura" en QuestPDF. Recibe el mismo JSON que la plantilla HTML factura.html.</summary>
internal sealed class FacturaQuestTemplate(IOptions<PdfGeneratorOptions> options) : IQuestPdfTemplate
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "factura";

    public Result<IDocument> Create(JsonElement data, PageSettings page)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return DocumentErrors.InvalidData("se esperaba un objeto JSON con la factura.");
        }

        FacturaModel? model;
        try
        {
            model = data.Deserialize<FacturaModel>(JsonOptions);
        }
        catch (JsonException ex)
        {
            return DocumentErrors.InvalidData(ex.Message);
        }

        return model is null
            ? DocumentErrors.InvalidData("factura vacia.")
            : Result.Success<IDocument>(new FacturaDocument(model, page, options.Value.DefaultFontFamily));
    }
}

internal sealed record FacturaParte(string? Nombre, string? Ruc, string? Direccion);

internal sealed record FacturaItem(string? Codigo, string? Descripcion, decimal Cantidad, decimal Precio)
{
    public decimal Total => Cantidad * Precio;
}

internal sealed record FacturaModel(
    string? Numero,
    string? Fecha,
    decimal? IvaPorcentaje,
    FacturaParte? Empresa,
    FacturaParte? Cliente,
    IReadOnlyList<FacturaItem>? Items,
    string? Observaciones)
{
    public IReadOnlyList<FacturaItem> Lineas => Items ?? [];

    public decimal Subtotal => Lineas.Sum(i => i.Total);

    public decimal Iva => Math.Round(Subtotal * (IvaPorcentaje ?? 15m) / 100m, 2);

    public decimal Total => Subtotal + Iva;
}

internal sealed class FacturaDocument(FacturaModel model, PageSettings page, string fontFamily) : IDocument
{
    private const string Primary = "#1f4e79";
    private const string Border = "#dddddd";

    public DocumentMetadata GetMetadata() => new() { Title = $"Factura {model.Numero}", Creator = "Danec.PdfGenerator" };

    public void Compose(IDocumentContainer container)
    {
        container.Page(p =>
        {
            var size = page.Size switch
            {
                DocPageSize.Letter => PageSizes.Letter,
                DocPageSize.Legal => PageSizes.Legal,
                _ => PageSizes.A4,
            };
            p.Size(page.IsLandscape ? size.Landscape() : size);
            p.Margin((float)page.MarginMm, Unit.Millimetre);
            p.DefaultTextStyle(TextStyle.Default.FontFamily(fontFamily).FontSize(9).FontColor("#222222"));

            p.Header().Element(ComposeHeader);
            p.Content().PaddingVertical(12).Element(ComposeContent);
            p.Footer().AlignCenter().Text(t =>
            {
                t.Span("Página ").FontSize(8).FontColor(Colors.Grey.Darken1);
                t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                t.Span(" de ").FontSize(8).FontColor(Colors.Grey.Darken1);
                t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private void ComposeHeader(IContainer container) => container.Row(row =>
    {
        row.RelativeItem().Column(col =>
        {
            col.Item().Text(model.Empresa?.Nombre ?? string.Empty).FontSize(18).Bold().FontColor(Primary);
            col.Item().Text($"RUC: {model.Empresa?.Ruc}");
            col.Item().Text(model.Empresa?.Direccion ?? string.Empty).FontSize(8).FontColor(Colors.Grey.Darken1);
        });

        row.ConstantItem(180).Border(1).BorderColor(Primary).Padding(6).Column(col =>
        {
            col.Item().Text("FACTURA").Bold().FontColor(Primary);
            col.Item().Text($"No. {model.Numero}");
            col.Item().Text($"Fecha: {model.Fecha}");
        });
    });

    private void ComposeContent(IContainer container) => container.Column(col =>
    {
        col.Spacing(12);

        col.Item().Background("#f3f6fa").Padding(6).Column(cliente =>
        {
            cliente.Item().Text("Cliente").Bold().FontColor(Primary);
            cliente.Item().Text($"{model.Cliente?.Nombre} — RUC/CI {model.Cliente?.Ruc}");
            cliente.Item().Text(model.Cliente?.Direccion ?? string.Empty);
        });

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(60);
                c.RelativeColumn();
                c.ConstantColumn(45);
                c.ConstantColumn(70);
                c.ConstantColumn(70);
            });

            table.Header(h =>
            {
                foreach (var (title, right) in new[] { ("Código", false), ("Descripción", false), ("Cant.", true), ("P. unitario", true), ("Total", true) })
                {
                    var cell = h.Cell().Background(Primary).Padding(4);
                    (right ? cell.AlignRight() : cell).Text(title).FontColor(Colors.White).Bold();
                }
            });

            foreach (var item in model.Lineas)
            {
                Cell(table).Text(item.Codigo ?? string.Empty);
                Cell(table).Text(item.Descripcion ?? string.Empty);
                Cell(table).AlignRight().Text(Number(item.Cantidad, "0.##"));
                Cell(table).AlignRight().Text(Number(item.Precio, "N2"));
                Cell(table).AlignRight().Text(Number(item.Total, "N2"));
            }
        });

        col.Item().AlignRight().Width(220).Column(totals =>
        {
            TotalRow(totals, "Subtotal", model.Subtotal, false);
            TotalRow(totals, $"IVA {Number(model.IvaPorcentaje ?? 15m, "0.##")} %", model.Iva, false);
            TotalRow(totals, "TOTAL", model.Total, true);
        });

        if (!string.IsNullOrWhiteSpace(model.Observaciones))
        {
            col.Item().Text($"Observaciones: {model.Observaciones}").FontSize(8).FontColor(Colors.Grey.Darken2);
        }
    });

    private static IContainer Cell(TableDescriptor table) =>
        table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(3).PaddingHorizontal(4);

    private static void TotalRow(ColumnDescriptor column, string label, decimal value, bool highlight) =>
        column.Item().PaddingVertical(2).Row(row =>
        {
            var left = row.RelativeItem().Text(label);
            var right = row.ConstantItem(90).AlignRight().Text(Number(value, "N2"));
            if (highlight)
            {
                left.Bold().FontSize(11).FontColor(Primary);
                right.Bold().FontSize(11).FontColor(Primary);
            }
        });

    private static string Number(decimal value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}
