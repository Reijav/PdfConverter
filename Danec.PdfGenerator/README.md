# Danec.PdfGenerator

Web API .NET 10 (Clean Architecture) que genera PDF a partir de **plantillas + parámetros** con seis motores gratuitos, uno por endpoint, para compararlos con los mismos datos.

| Endpoint (`POST /api/v1/pdf/...`) | Motor | Licencia | Plantilla | Cuándo usarlo |
|---|---|---|---|---|
| `itext` | iTextSharp 5.5.13.1 + XMLWorker | **AGPL** | `Templates/html/*.html` | XHTML + CSS 2.1; exige publicar el código o licencia comercial de iText |
| `questpdf` | QuestPDF **2022.12.15** (última MIT) | MIT | Clase C# `IQuestPdfTemplate` | Diseño en código, tipado; sin soporte oficial |
| `htmlrenderer` | Scriban + HtmlRenderer.PdfSharp 1.6.1 / PDFsharp 6.2 | BSD / MIT | `Templates/html/*.html` | HTML 4 / CSS 2, 100 % .NET, muy rápido |
| `htmlrenderer-fondo` | Igual que `htmlrenderer` + imagen de fondo estampada con PDFsharp | BSD / MIT | `Templates/html/*.html` + `Templates/fondo/*.png\|jpg` | Cartas y documentos con membrete o fondo corporativo |
| `overlay` | PDF base + texto superpuesto (PDFsharp) | MIT | `Templates/overlay/*.pdf` + `.json` | Certificados y formularios de diseño fijo |
| `puppeteer` | PuppeteerSharp 25 + Chromium | MIT | `Templates/html/*.html` | HTML5 / CSS3 completo, flexbox, grid |
| `playwright` | Microsoft.Playwright 1.63 + Chromium | Apache 2.0 | `Templates/html/*.html` | HTML5 / CSS3 completo, flexbox, grid |
| `docx` | MiniWord 0.9.2 + Gotenberg 8 (LibreOffice) | Apache 2.0 / MIT | `Templates/docx/*.docx` | El negocio edita la plantilla en Word; PDF fiel al .docx |
| `/api/v1/minipdf/pdf` | MiniWord 0.9.2 + MiniPdf 0.43 (en proceso) | Apache 2.0 | `Templates/docx/*.docx` | Plantilla Word sin Gotenberg; documentos simples sin "Página X de Y" ni PDF/A |

Medición local en Windows (factura de ejemplo, con el motor en caliente):

| Motor | Tiempo | Tamaño |
|---|---:|---:|
| iText | ~3 ms | 2 KB |
| HtmlRenderer | ~12 ms | 65 KB |
| Overlay | ~7 ms | 92 KB |
| QuestPDF | ~100 ms | 1,1 MB (2022.12 incrusta la fuente completa, sin subsetting) |
| Playwright | ~250 ms | 57 KB |
| Puppeteer | ~440 ms | 72 KB |

La primera llamada a Chromium tarda varios segundos porque arranca el navegador; después se reutiliza.

## Solicitud

```json
{
  "template": "factura",
  "data": { "numero": "001-001-000012345", "cliente": { "nombre": "Juan Pérez" }, "items": [ ... ] },
  "page": { "size": "A4", "orientation": "Portrait", "marginMm": 15 },
  "fileName": "factura-001"
}
```

- `template` **o** `html`: `html` es una plantilla Scriban en línea y solo la aceptan los motores HTML.
- `data`: JSON libre. Los textos se **escapan como HTML** automáticamente, así que un parámetro no puede inyectar etiquetas.
- `?inline=true`: devuelve `Content-Disposition: inline` para verlo en el navegador.
- Cabeceras de respuesta: `X-Pdf-Engine` y `X-Render-Time-Ms`.
- Errores en ProblemDetails (RFC 9457): 400 para validación o sintaxis de plantilla, 404 para plantilla inexistente y 500 para fallo del motor.

Otros endpoints: `GET /pdf/templates`, `GET /pdf/templates/{name}/sample` y `POST /pdf/preview-html`, que devuelve el HTML combinado y sirve para depurar plantillas.

## Plantillas

```
src/Danec.PdfGenerator.Api/Templates/
├── html/factura.html          # Scriban: {{ cliente.nombre }}, {{ for i in items }}...{{ end }}, {{ x | math.format "N2" }}
├── overlay/certificado.pdf    # PDF de fondo (diseñado con cualquier herramienta)
├── overlay/certificado.json   # campos: key, x, y (pt desde arriba-izquierda), fontSize, bold, align, color, text, required
├── samples/*.json             # datos de ejemplo por plantilla
└── requests/*.json            # cuerpos de ejemplo para el archivo .http
```

- Las plantillas HTML se recargan solas al editar el archivo.
- **Variable reservada `pdf`**: `pdf.motor` (IText, HtmlRenderer, Puppeteer, Playwright o Preview) y `pdf.css3` (true en Chromium). Sirve para adaptar la plantilla a lo que soporta cada motor. En `POST /pdf/preview-html` se puede simular con `"engine": "Playwright"`.
- **Marca de agua en `factura.html`**: envía `"marcaAgua": "BORRADOR"` (o COPIA, ANULADA...) en `data`. Puppeteer y Playwright dibujan una marca diagonal translúcida que se repite en cada página. HtmlRenderer e iText, que no soportan `transform` ni `opacity`, muestran una franja horizontal clara arriba del documento. Si no envías el campo, no se dibuja.
- Para que una plantilla funcione en los cuatro motores HTML, usa **CSS 2 y tablas**. Flexbox y grid solo funcionan en Puppeteer y Playwright.
- Para agregar una plantilla QuestPDF, crea una clase `IQuestPdfTemplate` y regístrala en `Infrastructure/DependencyInjection.cs`.

## Ejecutar en local

```powershell
dotnet build -c Release
# Chromium para Playwright y Puppeteer (una vez por versión de Microsoft.Playwright)
.\src\Danec.PdfGenerator.Api\bin\Release\net10.0\.playwright\node\win32_x64\node.exe `
  .\src\Danec.PdfGenerator.Api\bin\Release\net10.0\.playwright\package\cli.js install chromium
dotnet run --project src/Danec.PdfGenerator.Api        # http://localhost:5080/scalar
./scripts/smoke-test.ps1                               # prueba los 6 motores -> ./out
dotnet test --solution Danec.PdfGenerator.slnx -c Release
```

Puppeteer reutiliza el Chromium de Playwright (`%LOCALAPPDATA%\ms-playwright`). Si no lo encuentra, descarga Chrome en `Pdf:Chromium:DownloadPath`.

## Plantillas Word (MiniWord + Gotenberg)

| Endpoint | Devuelve |
|---|---|
| `POST /api/v1/word` | El `.docx` rellenado, editable |
| `POST /api/v1/pdf/docx` | El PDF: MiniWord rellena el `.docx` y Gotenberg lo convierte con LibreOffice |

Mismo cuerpo que el resto: `{ "template": "factura", "data": { ... }, "fileName": "..." }`.

- Marcadores en Word: `{{numero}}`. Los objetos anidados se aplanan con `_`, por ejemplo `data.empresa.nombre` pasa a `{{empresa_nombre}}`.
- Tablas: una fila con `{{items.codigo}}`, `{{items.precio}}`… se repite por cada elemento de `items`.
- Escribe cada marcador de una sola vez, sin cambiar formato a mitad. Si Word lo parte en varios fragmentos internos, MiniWord no lo encuentra.
- MiniWord no calcula ni formatea. Los valores derivados (totales, 2 decimales) van en un `IWordTemplateEnricher` por plantilla, como `FacturaWordEnricher`.
- En el PDF, el tamaño de página y los márgenes los define el `.docx`. De `page` solo se aplica `orientation`.

Gotenberg es un contenedor aparte y escucha en el puerto **3001** (`--api-port=3001`). En local: `docker run -d --name gotenberg -p 3001:3001 gotenberg/gotenberg:8 gotenberg --api-port=3001`, o `docker compose up`, que levanta la API y Gotenberg. En Container Apps va como sidecar con el mismo argumento, en `http://localhost:3001`. Se configura con `Pdf:Gotenberg:BaseUrl` y `TimeoutSeconds`. Si Gotenberg no responde, `/pdf/docx` devuelve 500 `Documents.ConverterUnavailable` y el resto de motores sigue funcionando.

## Plantillas Word sin Gotenberg (MiniWord + MiniPdf)

| Endpoint | Devuelve |
|---|---|
| `POST /api/v1/minipdf/docx` | El `.docx` rellenado por MiniWord (mismo resultado que `/api/v1/word`) |
| `POST /api/v1/minipdf/pdf` | El PDF: MiniWord rellena el `.docx` y MiniPdf lo convierte dentro del proceso. Admite `?inline=true` |

Cuerpo: `{ "template": "factura", "data": { ... }, "fileName": "..." }`. Usa las mismas plantillas y reglas de marcadores que la sección anterior. El tamaño de página y los márgenes los define el `.docx`.

- No necesita Gotenberg ni licencia comercial (MiniPdf, Apache 2.0). En Linux la factura tarda ~60–80 ms en caliente.
- Limitaciones de MiniPdf 0.x: no resuelve campos de Word (el pie "Página X de Y" sale vacío), no dibuja bordes de párrafo y no genera PDF/A. El espaciado vertical es algo más holgado que en Word. Detalle en el anexo ADR-GDOC-001-A.
- Fuentes: fuera de Windows, al iniciar se registran Arial, Helvetica, Calibri, Times New Roman, Georgia, Courier New y Consolas con su equivalente Liberation (`MiniPdfFontSetup`). Sin eso MiniPdf usa Helvetica sin incrustar y el PDF puede pesar varios MB. Para fuentes corporativas use `Pdf:MiniPdf:Fonts`.
- La conversión usa CPU del proceso de la API; `Pdf:MiniPdf:MaxConcurrency` limita las conversiones simultáneas.

## Imagen de fondo (`POST /api/v1/pdf/htmlrenderer-fondo`)

Genera el PDF igual que `/pdf/htmlrenderer` y **después** estampa una imagen de `Templates/fondo` detrás del contenido. PDFsharp usa `XGraphicsPdfPageOptions.Prepend`: la imagen se inserta al inicio del flujo de cada página, por lo que el texto, los bordes y los rellenos de tabla quedan visibles encima. La imagen se incrusta una sola vez y todas las páginas la referencian.

```json
{
  "template": "factura",
  "data": { ... },
  "fondo": { "imagen": "fondo-de-cartas", "paginas": "Todas", "ajuste": "Ancho" }
}
```

| Campo | Valores | Por defecto |
|---|---|---|
| `fondo.imagen` | Nombre del archivo en `Templates/fondo` **sin extensión** (`.png`, `.jpg` o `.jpeg`). Solo letras, números, `-` y `_` | `Pdf:Fondo:Default` (`fondo-de-cartas`) |
| `fondo.paginas` | `Todas`, `Primera` | `Todas` |
| `fondo.ajuste` | `Ancho` (todo el ancho, alto proporcional, arriba), `Pagina` (estirada a la hoja), `Centrado` (tamaño natural, se reduce si no cabe) | `Ancho` |

- `fondo` es opcional; sin él se aplica la imagen por defecto en todas las páginas.
- La plantilla debe tener fondo transparente: un `background` en `body` o en un contenedor de página completa tapa la imagen.
- Para que el texto no quede sobre el membrete, ajuste `page.marginMm` o deje espacio en la plantilla.
- Respuesta: además de `X-Pdf-Engine` y `X-Render-Time-Ms`, devuelve `X-Background` (archivo usado) y `X-Background-Ms` (tiempo del estampado).
- Errores: `Documents.BackgroundNameInvalid` (400), `Documents.BackgroundInvalid` (400: no es PNG/JPEG o supera `Pdf:Fondo:MaxBytes`), `Documents.BackgroundNotFound` (404).
- Las imágenes se cachean en memoria; para cambiar una hay que redesplegar o reiniciar.

## Inyección de dependencias

Todo se registra de forma explícita, sin escaneo por reflexión:

| Dónde | Qué | Ciclo de vida |
|---|---|---|
| `Application/DependencyInjection.cs` | Un handler por caso de uso (`GeneratePdfHandler`, `PreviewHtmlHandler`, `ListTemplatesHandler`, `GetTemplateSampleHandler`) | Scoped |
| `Infrastructure/DependencyInjection.cs` | Plantillas, navegadores Chromium y los 6 motores como *keyed services* (clave = `PdfEngine`) + `IPdfEngineFactory` | Singleton |
| `Api/Program.cs` | `api.MapPdfEndpoints()`: los endpoints reciben el handler concreto como parámetro | — |

Flujo: endpoint → `GeneratePdfHandler` → `IPdfEngineFactory.Get(engine)` → motor.

Para agregar un motor: crea la clase `IPdfEngine`, agrega el valor al enum `PdfEngine`, regístralo con `AddKeyedSingleton<IPdfEngine, TuMotor>(PdfEngine.TuMotor)` y añade su ruta en `PdfEndpoints.Engines`. El test `Cada_motor_tiene_su_adaptador_registrado_con_la_clave_correcta` falla si te olvidas del registro.

## Postman

La carpeta `postman/` contiene la colección (32 peticiones y 110 aserciones) y dos entornos: **Local** (puerto 5080) y **Docker** (puerto 8081).

- En Postman: *Import* de los tres archivos, elige el entorno y usa *Run collection*. Para guardar un PDF usa **Send and Download**.
- Por línea de comandos, con la API corriendo:

```powershell
npx -y newman run postman\Danec.PdfGenerator.postman_collection.json -e postman\Danec.PdfGenerator.Local.postman_environment.json
```

## Docker / Azure Container Apps

```powershell
docker build -t danec-pdfgenerator .
docker run --rm -p 8080:8080 danec-pdfgenerator
```

La imagen parte de `aspnet:10.0` (Ubuntu), no de chiseled, porque Chromium necesita librerías del sistema. Instala un solo Chromium, compartido por los dos motores, junto con las fuentes Liberation, DejaVu y fontconfig. La imagen ocupa unos **585 MB** comprimida y unos 2,2 GB en disco. En el contenedor Linux se validó que los seis motores funcionan (en caliente: iText 4 ms, Overlay 4 ms, HtmlRenderer 9 ms, QuestPDF 35 ms, Playwright 110 ms y Puppeteer 250 ms). Si no necesitas los motores Chromium, quítalos y puedes volver a una imagen chiseled.

Container Apps: asigna al menos 1 CPU y 2 GiB si usas Chromium, y ajusta `Pdf__Chromium__MaxConcurrency`.

## Configuración (`Pdf`)

| Clave | Default | Descripción |
|---|---|---|
| `TemplatesPath` | `Templates` | Carpeta de plantillas (relativa a la app o absoluta) |
| `DefaultFontFamily` | `Arial` | Fuente de QuestPDF. En Docker: `Liberation Sans` |
| `FontsPath` | — | Carpeta extra de `.ttf` para PDFsharp |
| `Chromium:ExecutablePath` | — | Chrome/Chromium propio |
| `Chromium:MaxConcurrency` | 4 | Pestañas simultáneas por motor |
| `Chromium:TimeoutSeconds` | 30 | |
| `Chromium:AllowExternalResources` | `false` | `false` bloquea toda petición de red de la plantilla (anti-SSRF): usa imágenes en `data:` URI |
| `Chromium:Args` | `--no-sandbox`, `--disable-dev-shm-usage`, `--disable-gpu` | |
| `MiniPdf:MaxConcurrency` | 4 | Conversiones MiniPdf simultáneas (1 a 32) |
| `MiniPdf:Fonts` | `{}` | Familia de Word → archivo `.ttf` (absoluto o relativo a `FontsPath`), p. ej. `"Arial": "/fonts/arial.ttf"` |

## Decisiones y trampas resueltas

- **QuestPDF fijado a `[2022.12.15]`.** Desde la 2023.4 la licencia es comercial para empresas con más de USD 1 M de ingresos anuales. No lo actualices.
- **iTextSharp 5.5.13.1 + XMLWorker** (fijado con `[5.5.13.1]`). Solo trae binarios .NET Framework: en .NET 10 se carga por compatibilidad (NU1701 suprimido) y requiere `CodePagesEncodingProvider`, que se registra en `AddInfrastructure`. XMLWorker exige XHTML bien formado.
- **Fuentes de PDFsharp.** Hay tres problemas y `PdfSharpFontSetup` resuelve los tres al arrancar:
  1. HtmlRenderer registra su propio `FontResolver` global la primera vez que se usa, y PDFsharp lo prohíbe si otro motor ya usó fuentes. Eso provocaba un `TypeInitializationException` que dependía del orden de las peticiones. La solución fuerza su inicialización al arrancar y encadena nuestro resolvedor encima. Lo cubre el test `HtmlRenderer_funciona_aunque_Overlay_use_PDFsharp_primero`.
  2. Para las familias conocidas (Arial, Helvetica, sans-serif, Times, Courier) manda nuestro mapeo: Arial/Times/Courier en Windows y Liberation o DejaVu en Linux.
  3. En Linux, HtmlRenderer sustituía Arial por cualquier fuente instalada (Tuffy). Se corrige con `PdfGenerator.AddFontFamilyMapping` hacia Liberation.
- **Validación de Minimal APIs en .NET 10.** El validador falla al recorrer `JsonElement`, por eso `data` lleva `[SkipValidation]` (experimental, ASP0029).
- **Licencia AGPL (iText 5).** Usar iTextSharp 5 en un servicio obliga a publicar el código fuente de la aplicación completa (también cuando solo se expone como API), salvo que se compre una licencia comercial de iText. Revísalo con legal antes de ir a producción; la alternativa sin esa obligación era el port LGPL de la 4.1.6.
