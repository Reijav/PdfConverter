using GlobalProperties = SelectPdf.Universal.GlobalProperties;

namespace Danec.PdfGenerator.Infrastructure.Engines.SelectPdfUniversal;

/// <summary>Configuracion estatica (a nivel de proceso) de SelectPdf.Universal. Se llama una vez al arrancar.</summary>
internal static class SelectPdfSetup
{
    public static void Configure(SelectPdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // La clave llega por configuracion (Key Vault, variable Pdf__SelectPdf__LicenseKey o user-secrets): nunca en el repositorio
        if (!string.IsNullOrWhiteSpace(options.LicenseKey))
        {
            GlobalProperties.LicenseKey = options.LicenseKey;
        }

        // Una plantilla o un parametro no debe poder leer archivos del servidor (file://)
        GlobalProperties.ForceDenyLocalFileAccess = !options.AllowLocalFiles;
    }
}
