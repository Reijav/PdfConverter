namespace Danec.PdfGenerator.Infrastructure.Engines.Docx;

/// <summary>
/// Convierte los parametros JSON al modelo que entiende MiniWord (Dictionary plano):
/// <list type="bullet">
/// <item>objetos anidados se aplanan con "_": <c>{"empresa":{"nombre":"X"}}</c> -> <c>{{empresa_nombre}}</c></item>
/// <item>arreglos de objetos se convierten en filas de tabla: <c>items</c> -> <c>{{items.codigo}}</c> en una fila de tabla</item>
/// <item>numeros se escriben con punto decimal (cultura invariante); booleanos como "Sí"/"No"</item>
/// </list>
/// </summary>
internal static class WordTemplateModel
{
    public static Dictionary<string, object> FromJson(JsonElement data)
    {
        var model = new Dictionary<string, object>(StringComparer.Ordinal);
        if (data.ValueKind == JsonValueKind.Object)
        {
            Flatten(data, prefix: null, model);
        }

        return model;
    }

    public static string Format(decimal value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static void Flatten(JsonElement element, string? prefix, Dictionary<string, object> target)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix is null ? property.Name : $"{prefix}_{property.Name}";
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    Flatten(property.Value, key, target);
                    break;
                case JsonValueKind.Array:
                    target[key] = ToRows(property.Value);
                    break;
                default:
                    if (ToText(property.Value) is { } text)
                    {
                        target[key] = text;
                    }

                    break;
            }
        }
    }

    private static List<Dictionary<string, object>> ToRows(JsonElement array) =>
    [
        .. array.EnumerateArray().Select(item =>
        {
            var row = new Dictionary<string, object>(StringComparer.Ordinal);
            if (item.ValueKind == JsonValueKind.Object)
            {
                Flatten(item, prefix: null, row);
            }
            else if (ToText(item) is { } text)
            {
                row["valor"] = text;
            }

            return row;
        }),
    ];

    private static string? ToText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "Sí",
        JsonValueKind.False => "No",
        _ => null,
    };
}
