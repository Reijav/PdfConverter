using System.Collections.Concurrent;
using Danec.PdfGenerator.Application.Documents.GeneratePdfWithBackground;
using Danec.PdfGenerator.Infrastructure.Templates;

namespace Danec.PdfGenerator.Infrastructure.Backgrounds;

/// <summary>
/// Imagenes de fondo en Templates/fondo/{nombre}.png|.jpg|.jpeg. El nombre ya viene validado como
/// <see cref="TemplateName"/> (sin separadores ni ".."), asi que la ruta no puede salir de la carpeta.
/// Las imagenes se cachean en memoria: viajan con la app y no cambian sin redespliegue.
/// </summary>
internal sealed class FileSystemBackgroundImageStore(TemplatePaths paths, IOptions<PdfGeneratorOptions> options)
    : IBackgroundImageStore
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg"];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    private readonly ConcurrentDictionary<string, BackgroundImage> _cache = new(StringComparer.Ordinal);

    public string DefaultName => options.Value.Fondo.Default;

    public async Task<Result<BackgroundImage>> GetAsync(TemplateName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_cache.TryGetValue(name.Value, out var cached))
        {
            return cached;
        }

        var file = Extensions
            .Select(ext => Path.Combine(paths.BackgroundDirectory, name.Value + ext))
            .FirstOrDefault(File.Exists);
        if (file is null)
        {
            return DocumentErrors.BackgroundNotFound(name.Value);
        }

        var maxBytes = options.Value.Fondo.MaxBytes;
        if (new FileInfo(file).Length > maxBytes)
        {
            return DocumentErrors.BackgroundInvalid(
                $"'{Path.GetFileName(file)}' supera el maximo de {maxBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes.");
        }

        var content = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
        if (!content.AsSpan().StartsWith(PngSignature) && !content.AsSpan().StartsWith(JpegSignature))
        {
            return DocumentErrors.BackgroundInvalid($"'{Path.GetFileName(file)}' no es una imagen PNG o JPEG.");
        }

        var image = new BackgroundImage(name.Value, Path.GetFileName(file), content);
        _cache.TryAdd(name.Value, image);
        return image;
    }
}
