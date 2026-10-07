using System.Text;
using Scriban.Runtime;

namespace Danec.PdfGenerator.Infrastructure.Templates;

/// <summary>
/// Convierte los parametros JSON en variables Scriban. Los textos se escapan como HTML
/// (&amp; &lt; &gt; " ') para que un parametro nunca pueda inyectar etiquetas o scripts en la plantilla.
/// Ademas expone la variable reservada <c>pdf</c>:
/// <list type="bullet">
/// <item><c>pdf.motor</c>: "IText", "HtmlRenderer", "Puppeteer", "Playwright" o "Preview".</item>
/// <item><c>pdf.css3</c>: true si el motor es Chromium (flexbox, grid, transform, opacity, position:fixed).</item>
/// </list>
/// </summary>
internal static class ScribanJson
{
    public const string ReservedVariable = "pdf";

    public static ScriptObject ToGlobals(JsonElement data, PdfEngine? engine = null)
    {
        var globals = new ScriptObject();

        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in data.EnumerateObject())
            {
                globals.SetValue(property.Name, ToValue(property.Value), readOnly: false);
            }
        }
        else if (data.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            globals.SetValue("data", ToValue(data), readOnly: false);
        }

        var pdf = new ScriptObject();
        pdf.SetValue("motor", engine?.ToString() ?? "Preview", readOnly: true);
        pdf.SetValue("css3", engine is PdfEngine.Puppeteer or PdfEngine.Playwright or PdfEngine.SelectPdf, readOnly: true);
        globals.SetValue(ReservedVariable, pdf, readOnly: true);

        return globals;
    }

    private static object? ToValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ToObject(element),
        JsonValueKind.Array => ToArray(element),
        JsonValueKind.String => EncodeHtml(element.GetString()),
        JsonValueKind.Number => ToNumber(element),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    private static ScriptObject ToObject(JsonElement element)
    {
        var result = new ScriptObject();
        foreach (var property in element.EnumerateObject())
        {
            result.SetValue(property.Name, ToValue(property.Value), readOnly: false);
        }

        return result;
    }

    private static ScriptArray ToArray(JsonElement element)
    {
        var result = new ScriptArray();
        foreach (var item in element.EnumerateArray())
        {
            result.Add(ToValue(item));
        }

        return result;
    }

    private static object ToNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var integer))
        {
            return integer;
        }

        return element.TryGetDecimal(out var number) ? number : element.GetDouble();
    }

    private static string? EncodeHtml(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.AsSpan().IndexOfAny("&<>\"'") < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 16);
        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }
}
