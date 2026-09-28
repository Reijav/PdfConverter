using Danec.PdfGenerator.Infrastructure.Templates;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace Danec.PdfGenerator.Infrastructure.Engines.Overlay;

/// <summary>
/// PDF base + texto superpuesto (PDFsharp, MIT). Un disenador crea el PDF de fondo con cualquier
/// herramienta y un archivo .json indica donde escribir cada parametro. Muy rapido, pero sin contenido
/// de longitud variable (tablas de N filas).
/// </summary>
internal sealed partial class OverlayPdfEngine(TemplatePaths paths, ILogger<OverlayPdfEngine> logger) : IPdfEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PdfEngine Engine => PdfEngine.Overlay;

    public async Task<Result<byte[]>> RenderAsync(PdfRenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Template.Name is not { } name)
        {
            return DocumentErrors.InlineHtmlNotSupported(Engine);
        }

        var pdfFile = paths.OverlayPdf(name);
        var mapFile = paths.OverlayMap(name);
        if (!File.Exists(pdfFile) || !File.Exists(mapFile))
        {
            return DocumentErrors.TemplateNotFound(name.Value, TemplateKind.Overlay);
        }

        OverlayMap? map;
        try
        {
            var stream = File.OpenRead(mapFile);
            await using (stream.ConfigureAwait(false))
            {
                map = await JsonSerializer.DeserializeAsync<OverlayMap>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException ex)
        {
            return DocumentErrors.TemplateSyntax($"{Path.GetFileName(mapFile)}: {ex.Message}");
        }

        var fields = map?.Fields ?? [];
        var missing = fields.Where(f => f.Required && JsonData.Find(request.Data, f.Key) is null).Select(f => f.Key).ToList();
        if (missing.Count > 0)
        {
            return DocumentErrors.InvalidData($"faltan los campos obligatorios: {string.Join(", ", missing)}.");
        }

        var basePdf = await File.ReadAllBytesAsync(pdfFile, cancellationToken).ConfigureAwait(false);
        try
        {
            return Stamp(basePdf, map?.FontFamily, fields, request.Data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, name.Value);
            return DocumentErrors.RenderFailed(Engine, ex.Message);
        }
    }

    private static byte[] Stamp(byte[] basePdf, string? defaultFont, IReadOnlyList<OverlayField> fields, JsonElement data)
    {
        using var input = new MemoryStream(basePdf);
        using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        foreach (var pageFields in fields.GroupBy(f => f.Page))
        {
            if (pageFields.Key < 1 || pageFields.Key > document.PageCount)
            {
                continue;
            }

            using var gfx = XGraphics.FromPdfPage(document.Pages[pageFields.Key - 1], XGraphicsPdfPageOptions.Append);
            foreach (var field in pageFields)
            {
                var text = field.Render(JsonData.Find(data, field.Key));
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var style = (field.Bold, field.Italic) switch
                {
                    (true, true) => XFontStyleEx.BoldItalic,
                    (true, false) => XFontStyleEx.Bold,
                    (false, true) => XFontStyleEx.Italic,
                    _ => XFontStyleEx.Regular,
                };
                var font = new XFont(field.FontFamily ?? defaultFont ?? "Arial", field.FontSize, style);
                var format = field.Align switch
                {
                    _ when string.Equals(field.Align, "center", StringComparison.OrdinalIgnoreCase) => XStringFormats.BaseLineCenter,
                    _ when string.Equals(field.Align, "right", StringComparison.OrdinalIgnoreCase) => XStringFormats.BaseLineRight,
                    _ => XStringFormats.BaseLineLeft,
                };

                gfx.DrawString(text, font, new XSolidBrush(ParseColor(field.Color)), new XPoint(field.X, field.Y), format);
            }
        }

        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    private static XColor ParseColor(string? hex)
    {
        if (hex is { Length: 7 } && hex[0] == '#'
            && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return XColor.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        return XColors.Black;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo el estampado de la plantilla overlay {Template}")]
    private static partial void LogFailed(ILogger logger, Exception exception, string template);
}

/// <summary>Mapa de campos de una plantilla overlay (archivo overlay/{nombre}.json).</summary>
internal sealed record OverlayMap(string? FontFamily, IReadOnlyList<OverlayField>? Fields);

/// <summary>
/// Un campo a escribir. Coordenadas en puntos (1 cm = 28.35 pt) desde la esquina superior izquierda;
/// Y es la linea base del texto. Align: left | center | right (X es el punto de anclaje).
/// </summary>
internal sealed record OverlayField(
    string Key,
    double X,
    double Y,
    int Page = 1,
    double FontSize = 12,
    bool Bold = false,
    bool Italic = false,
    string? Align = null,
    string? Color = null,
    string? FontFamily = null,
    string? Format = null,
    string? Text = null,
    bool Required = false)
{
    /// <summary>Texto final: el valor (con Format si es numero) insertado en Text donde diga {value}.</summary>
    public string? Render(JsonElement? value)
    {
        if (value is not { } v)
        {
            return null;
        }

        var raw = v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number when Format is not null && v.TryGetDecimal(out var number) => number.ToString(Format, CultureInfo.InvariantCulture),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "Sí",
            JsonValueKind.False => "No",
            _ => null,
        };

        return raw is null || Text is null ? raw : Text.Replace("{value}", raw, StringComparison.Ordinal);
    }
}
