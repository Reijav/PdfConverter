namespace Danec.PdfGenerator.Domain.Documents;

/// <summary>Paginas del PDF que reciben la imagen de fondo.</summary>
public enum BackgroundPages
{
    /// <summary>Todas las paginas (por defecto).</summary>
    Todas,

    /// <summary>Solo la primera pagina (p.ej. membrete de carta).</summary>
    Primera,
}

/// <summary>Como se ajusta la imagen de fondo a la pagina.</summary>
public enum BackgroundFit
{
    /// <summary>Ocupa todo el ancho, alto proporcional, anclada arriba (por defecto).</summary>
    Ancho,

    /// <summary>Estirada a la pagina completa (puede deformarse si la proporcion no coincide).</summary>
    Pagina,

    /// <summary>Tamano natural centrada; si no cabe se reduce manteniendo la proporcion.</summary>
    Centrado,
}

/// <summary>Imagen de fondo a estampar detras del contenido de un PDF ya generado.</summary>
/// <param name="Image">Nombre de la imagen del catalogo Templates/fondo (sin extension).</param>
/// <param name="Pages">Paginas que la reciben.</param>
/// <param name="Fit">Ajuste a la pagina.</param>
public sealed record BackgroundSpec(TemplateName Image, BackgroundPages Pages, BackgroundFit Fit)
{
    /// <summary>
    /// Valida la solicitud. El nombre usa las mismas reglas que <see cref="TemplateName"/>
    /// (letras, numeros, '-' y '_'), por lo que no puede salir de la carpeta de fondos.
    /// </summary>
    public static Result<BackgroundSpec> Create(string? image, string defaultImage, BackgroundPages? pages, BackgroundFit? fit)
    {
        var requested = string.IsNullOrWhiteSpace(image) ? defaultImage : image;
        var name = TemplateName.Create(requested);
        if (name.IsFailure)
        {
            return DocumentErrors.BackgroundNameInvalid(requested ?? string.Empty);
        }

        if (pages is { } p && !Enum.IsDefined(p))
        {
            return DocumentErrors.BackgroundInvalid("'paginas' admite: Todas, Primera.");
        }

        if (fit is { } f && !Enum.IsDefined(f))
        {
            return DocumentErrors.BackgroundInvalid("'ajuste' admite: Ancho, Pagina, Centrado.");
        }

        return new BackgroundSpec(name.Value, pages ?? BackgroundPages.Todas, fit ?? BackgroundFit.Ancho);
    }
}
