namespace Danec.PdfGenerator.Infrastructure.Fonts;

/// <summary>
/// MiniPdf busca las fuentes por nombre de familia. En Linux no existen Arial, Times New Roman, etc.:
/// sin registrarlas usa Helvetica sin incrustar y puede incrustar completa otra fuente del sistema
/// (en la prueba de factibilidad, un PDF de 15 paginas paso de 10,5 MB a 856 KB al registrar las fuentes).
/// El registro es estatico y de todo el proceso, por eso se hace una sola vez al iniciar.
/// </summary>
internal static class MiniPdfFontSetup
{
    private static readonly Lock Gate = new();
    private static bool _configured;

    /// <summary>Familias de Office -> archivo Liberation equivalente en metricas (solo si el archivo existe).</summary>
    private static readonly (string Family, string File)[] LinuxDefaults =
    [
        ("Arial", "LiberationSans-Regular.ttf"),
        ("Helvetica", "LiberationSans-Regular.ttf"),
        ("Calibri", "LiberationSans-Regular.ttf"),
        ("Times New Roman", "LiberationSerif-Regular.ttf"),
        ("Georgia", "LiberationSerif-Regular.ttf"),
        ("Courier New", "LiberationMono-Regular.ttf"),
        ("Consolas", "LiberationMono-Regular.ttf"),
    ];

    private static readonly string[] LinuxFontDirectories =
    [
        "/usr/share/fonts/truetype/liberation",
        "/usr/share/fonts/truetype/liberation2",
        "/usr/share/fonts/liberation",
    ];

    /// <summary>Registra las fuentes configuradas y, fuera de Windows, las equivalencias Liberation.</summary>
    /// <returns>Familias registradas (para diagnostico).</returns>
    public static IReadOnlyList<string> Configure(MiniPdfOptions options, string? fontsPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (Gate)
        {
            if (_configured)
            {
                return [];
            }

            _configured = true;
            var registered = new List<string>();
            var explicitFamilies = new HashSet<string>(options.Fonts.Keys, StringComparer.OrdinalIgnoreCase);

            // 1) Mapeo explicito de configuracion: Pdf:MiniPdf:Fonts { "Arial": "/ruta/archivo.ttf" }
            foreach (var (family, file) in options.Fonts)
            {
                var path = Path.IsPathRooted(file) || fontsPath is null ? file : Path.Combine(fontsPath, file);
                if (File.Exists(path))
                {
                    MiniSoftware.MiniPdf.RegisterFont(family, File.ReadAllBytes(path));
                    registered.Add(family);
                }
            }

            // 2) Linux / contenedores: equivalencias Liberation para las familias mas comunes de Word
            if (!OperatingSystem.IsWindows())
            {
                string[] directories = fontsPath is null ? LinuxFontDirectories : [fontsPath, .. LinuxFontDirectories];
                foreach (var (family, file) in LinuxDefaults.Where(d => !explicitFamilies.Contains(d.Family)))
                {
                    var path = directories.Select(d => Path.Combine(d, file)).FirstOrDefault(File.Exists);
                    if (path is not null)
                    {
                        MiniSoftware.MiniPdf.RegisterFont(family, File.ReadAllBytes(path));
                        registered.Add(family);
                    }
                }
            }

            return registered;
        }
    }
}
