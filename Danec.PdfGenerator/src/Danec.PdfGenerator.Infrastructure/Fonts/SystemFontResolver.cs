using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using PdfSharp.Fonts;

namespace Danec.PdfGenerator.Infrastructure.Fonts;

/// <summary>
/// PDFsharp 6 (build Core, multiplataforma) no busca fuentes del sistema por si solo.
/// 1) Consulta el resolvedor de HtmlRenderer (fuentes reales por nombre: Windows y fc-list en Linux).
/// 2) Si no hay coincidencia, agrupa la familia en sans / serif / mono y busca Arial/Times/Courier (Windows),
///    Liberation (Linux, metricamente compatibles) o DejaVu. Asi "sans-serif" o "Arial" funcionan en contenedores.
/// </summary>
internal sealed class SystemFontResolver : IFontResolver
{
    private const string SystemPrefix = "sys:";

    // Por grupo: [regular, negrita, cursiva, negrita+cursiva] -> archivos alternativos
    private static readonly Dictionary<string, string[][]> Candidates = new(StringComparer.Ordinal)
    {
        ["sans"] =
        [
            ["arial.ttf", "LiberationSans-Regular.ttf", "DejaVuSans.ttf"],
            ["arialbd.ttf", "LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf"],
            ["ariali.ttf", "LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf"],
            ["arialbi.ttf", "LiberationSans-BoldItalic.ttf", "DejaVuSans-BoldOblique.ttf"],
        ],
        ["serif"] =
        [
            ["times.ttf", "LiberationSerif-Regular.ttf", "DejaVuSerif.ttf"],
            ["timesbd.ttf", "LiberationSerif-Bold.ttf", "DejaVuSerif-Bold.ttf"],
            ["timesi.ttf", "LiberationSerif-Italic.ttf", "DejaVuSerif-Italic.ttf"],
            ["timesbi.ttf", "LiberationSerif-BoldItalic.ttf", "DejaVuSerif-BoldItalic.ttf"],
        ],
        ["mono"] =
        [
            ["cour.ttf", "LiberationMono-Regular.ttf", "DejaVuSansMono.ttf"],
            ["courbd.ttf", "LiberationMono-Bold.ttf", "DejaVuSansMono-Bold.ttf"],
            ["couri.ttf", "LiberationMono-Italic.ttf", "DejaVuSansMono-Oblique.ttf"],
            ["courbi.ttf", "LiberationMono-BoldItalic.ttf", "DejaVuSansMono-BoldOblique.ttf"],
        ],
    };

    private readonly IFontResolver? _system;
    private readonly Lazy<Dictionary<string, string>> _index;
    private readonly ConcurrentDictionary<string, byte[]?> _cache = new(StringComparer.Ordinal);

    public SystemFontResolver(string? extraDirectory, IFontResolver? systemResolver)
    {
        _system = systemResolver is SystemFontResolver ? null : systemResolver;
        _index = new Lazy<Dictionary<string, string>>(() => BuildIndex(extraDirectory));
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool isItalic)
    {
        var style = (bold ? 1 : 0) + (isItalic ? 2 : 0);
        var knownGroup = KnownGroupOf(familyName);

        // 1) Familias conocidas (Arial, Helvetica, sans-serif, Times, Courier...): mapeo propio y predecible.
        //    El resolvedor de HtmlRenderer nunca devuelve null y en Linux sustituia Arial por cualquier fuente (p.ej. Tuffy).
        if (knownGroup is not null && ResolveOwn(knownGroup, style, bold, isItalic) is { } known)
        {
            return known;
        }

        // 2) Familias especificas instaladas en el sistema (Roboto, Segoe UI, etc.)
        if (ResolveWithSystem(familyName, bold, isItalic) is { } fromSystem)
        {
            return fromSystem;
        }

        // 3) Ultimo recurso: sans
        return ResolveOwn("sans", style, bold, isItalic);
    }

    private FontResolverInfo? ResolveOwn(string group, int style, bool bold, bool isItalic)
    {
        if (GetFont(Face(group, style)) is not null)
        {
            return new FontResolverInfo(Face(group, style));
        }

        // Sin la variante: PDFsharp simula negrita/cursiva sobre la regular
        return style != 0 && GetFont(Face(group, 0)) is not null
            ? new FontResolverInfo(Face(group, 0), bold, isItalic)
            : null;
    }

    public byte[]? GetFont(string faceName)
    {
        ArgumentNullException.ThrowIfNull(faceName);
        return faceName.StartsWith(SystemPrefix, StringComparison.Ordinal)
            ? _system?.GetFont(faceName[SystemPrefix.Length..])
            : _cache.GetOrAdd(faceName, Load);
    }

    private FontResolverInfo? ResolveWithSystem(string familyName, bool bold, bool isItalic)
    {
        try
        {
            var info = _system?.ResolveTypeface(familyName, bold, isItalic);
            return info is null
                ? null
                : new FontResolverInfo(SystemPrefix + info.FaceName, info.MustSimulateBold, info.MustSimulateItalic);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // p.ej. Linux sin fc-list: se usa la busqueda propia
            return null;
        }
    }

    private static string Face(string group, int style) => $"{group}#{style.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Grupo de las familias genericas o metricamente equivalentes; null para familias especificas.</summary>
    private static string? KnownGroupOf(string family)
    {
        var name = family ?? string.Empty;
        bool Has(string token) => name.Contains(token, StringComparison.OrdinalIgnoreCase);

        if (Has("mono") || Has("courier"))
        {
            return "mono";
        }

        if (Has("sans") || Has("arial") || Has("helvetica"))
        {
            return "sans";
        }

        return Has("serif") || Has("times") ? "serif" : null;
    }

    private byte[]? Load(string faceName)
    {
        var parts = faceName.Split('#');
        if (parts.Length != 2
            || !Candidates.TryGetValue(parts[0], out var styles)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var style)
            || style is < 0 or > 3)
        {
            return null;
        }

        foreach (var file in styles[style])
        {
            if (_index.Value.TryGetValue(file, out var path))
            {
                return File.ReadAllBytes(path);
            }
        }

        return null;
    }

    private static Dictionary<string, string> BuildIndex(string? extraDirectory)
    {
        var directories = new[]
        {
            extraDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"),
            "/usr/share/fonts",
            "/usr/local/share/fonts",
        };

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive };
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in directories.Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)))
        {
            foreach (var file in Directory.EnumerateFiles(directory!, "*.ttf", options))
            {
                index.TryAdd(Path.GetFileName(file), file);
            }
        }

        return index;
    }
}

internal static class PdfSharpFontSetup
{
    private const string HtmlRendererAdapter = "TheArtOfDev.HtmlRenderer.PdfSharp.Adapters.PdfSharpAdapter, HtmlRenderer.PdfSharp";
    private static readonly Lock Gate = new();
    private static bool _configured;

    /// <summary>
    /// GlobalFontSettings es estatico y NO puede cambiarse despues de usar la primera fuente.
    /// HtmlRenderer.PdfSharp asigna su propio resolvedor en el constructor estatico de su adaptador:
    /// si Overlay (PDFsharp) generaba un PDF antes, HtmlRenderer fallaba con TypeInitializationException.
    /// Por eso se fuerza su inicializacion al arrancar y luego se encadena nuestro resolvedor encima.
    /// </summary>
    public static void Configure(string? fontsPath)
    {
        lock (Gate)
        {
            if (_configured)
            {
                return;
            }

            if (Type.GetType(HtmlRendererAdapter, throwOnError: false) is { } adapter)
            {
                RuntimeHelpers.RunClassConstructor(adapter.TypeHandle);
            }

            if (!OperatingSystem.IsWindows())
            {
                MapHtmlRendererFamilies();
            }

            GlobalFontSettings.FontResolver = new SystemFontResolver(fontsPath, GlobalFontSettings.FontResolver);
            _configured = true;
        }
    }

    /// <summary>
    /// HtmlRenderer elige la familia CSS por su cuenta: si "Arial" no esta instalada usa cualquier otra (p.ej. Tuffy).
    /// En Linux se mapean las familias habituales a Liberation, metricamente compatible con Arial/Times/Courier.
    /// </summary>
    private static void MapHtmlRendererFamilies()
    {
        (string From, string To)[] mappings =
        [
            ("Arial", "Liberation Sans"), ("Helvetica", "Liberation Sans"), ("sans-serif", "Liberation Sans"),
            ("Times New Roman", "Liberation Serif"), ("Times", "Liberation Serif"), ("serif", "Liberation Serif"),
            ("Courier New", "Liberation Mono"), ("Courier", "Liberation Mono"), ("monospace", "Liberation Mono"),
        ];

        foreach (var (from, to) in mappings)
        {
            global::TheArtOfDev.HtmlRenderer.PdfSharp.PdfGenerator.AddFontFamilyMapping(from, to);
        }
    }
}
