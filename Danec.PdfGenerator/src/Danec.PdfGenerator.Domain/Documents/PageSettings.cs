namespace Danec.PdfGenerator.Domain.Documents;

public enum PageSize
{
    A4,
    Letter,
    Legal,
}

public enum PageOrientation
{
    Portrait,
    Landscape,
}

/// <summary>Configuracion de pagina comun a todos los motores.</summary>
public sealed record PageSettings
{
    public const double MaxMarginMm = 50;

    public static readonly PageSettings Default = new(PageSize.A4, PageOrientation.Portrait, 15);

    private PageSettings(PageSize size, PageOrientation orientation, double marginMm)
    {
        Size = size;
        Orientation = orientation;
        MarginMm = marginMm;
    }

    public PageSize Size { get; }

    public PageOrientation Orientation { get; }

    public double MarginMm { get; }

    public bool IsLandscape => Orientation == PageOrientation.Landscape;

    /// <summary>Margen en puntos PDF (1 pt = 1/72 pulgada).</summary>
    public double MarginPoints => MarginMm * 72d / 25.4d;

    public static Result<PageSettings> Create(PageSize? size, PageOrientation? orientation, double? marginMm)
    {
        if (size is { } s && !Enum.IsDefined(s))
        {
            return DocumentErrors.InvalidPageSize;
        }

        if (orientation is { } o && !Enum.IsDefined(o))
        {
            return DocumentErrors.InvalidOrientation;
        }

        var margin = marginMm ?? Default.MarginMm;
        if (double.IsNaN(margin) || margin < 0 || margin > MaxMarginMm)
        {
            return DocumentErrors.InvalidMargin(margin);
        }

        return new PageSettings(size ?? Default.Size, orientation ?? Default.Orientation, margin);
    }
}
