namespace Danec.PdfGenerator.Infrastructure.Templates;

internal static class JsonData
{
    /// <summary>Busca un valor por ruta con puntos: "cliente.nombre", "items.0.precio". Ignora mayusculas.</summary>
    public static JsonElement? Find(JsonElement root, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var current = root;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind == JsonValueKind.Object && TryGetProperty(current, segment, out var child))
            {
                current = child;
            }
            else if (current.ValueKind == JsonValueKind.Array
                     && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                     && index < current.GetArrayLength())
            {
                current = current[index];
            }
            else
            {
                return null;
            }
        }

        return current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : current;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }
}
