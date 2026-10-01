$ErrorActionPreference = 'Stop'
# Agrega (o reemplaza) la carpeta "7. Imagen de fondo" en la coleccion de Postman.
# Guardar este archivo como UTF-8 CON BOM: PowerShell 5.1 lee los .ps1 sin BOM como ANSI.
$root = Split-Path $PSScriptRoot -Parent
$file = Join-Path $root 'postman\Danec.PdfGenerator.postman_collection.json'
$factura = Get-Content (Join-Path $root 'src\Danec.PdfGenerator.Api\Templates\requests\factura-fondo.request.json') -Raw -Encoding UTF8
$j = Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json

function Body([string]$imagen, [string]$paginas, [string]$ajuste) {
    $fondo = '"fondo": { "imagen": "' + $imagen + '", "paginas": "' + $paginas + '", "ajuste": "' + $ajuste + '" }'
    $factura -replace '"fondo": \{[^}]*\}', $fondo
}

function Request([string]$name, [string]$raw, [bool]$inline, [string]$description, [string[]]$tests) {
    $url = '{{baseUrl}}/api/v1/pdf/htmlrenderer-fondo'
    $query = @()
    if ($inline) { $url += '?inline=true'; $query = @(@{ key = 'inline'; value = 'true' }) }
    [pscustomobject]@{
        name    = $name
        event   = @(@{ listen = 'test'; script = @{ type = 'text/javascript'; exec = $tests } })
        request = [pscustomobject]@{
            method      = 'POST'
            header      = @(@{ key = 'Content-Type'; value = 'application/json' })
            url         = [pscustomobject]@{ raw = $url; host = @('{{baseUrl}}'); path = @('api', 'v1', 'pdf', 'htmlrenderer-fondo'); query = $query }
            body        = [pscustomobject]@{ mode = 'raw'; raw = $raw; options = @{ raw = @{ language = 'json' } } }
            description = $description
        }
    }
}

$ok = @(
    'pm.test("HTTP 200", () => pm.response.to.have.status(200));',
    'pm.test("Content-Type application/pdf", () => pm.expect(pm.response.headers.get("Content-Type")).to.include("application/pdf"));',
    'pm.test("X-Pdf-Engine = HtmlRenderer", () => pm.expect(pm.response.headers.get("X-Pdf-Engine")).to.eql("HtmlRenderer"));',
    'pm.test("Empieza con %PDF-", () => pm.expect(pm.response.text().substring(0, 5)).to.eql("%PDF-"));',
    'pm.test("X-Background informado", () => pm.expect(pm.response.headers.get("X-Background")).to.match(/\.(png|jpe?g)$/));',
    'pm.test("Tiempos informados", () => {',
    '    pm.expect(Number(pm.response.headers.get("X-Render-Time-Ms"))).to.be.above(0);',
    '    pm.expect(Number(pm.response.headers.get("X-Background-Ms"))).to.be.at.least(0);',
    '});'
)
$inlineTest = 'pm.test("Content-Disposition inline", () => pm.expect(pm.response.headers.get("Content-Disposition")).to.include("inline"));'

function Problem([int]$status, [string]$code) {
    $t = @(
        "pm.test(""HTTP $status"", () => pm.response.to.have.status($status));",
        'pm.test("ProblemDetails (RFC 9457)", () => pm.expect(pm.response.headers.get("Content-Type")).to.include("application/problem+json"));'
    )
    if ($code) { $t += "pm.test(""errorCode = $code"", () => pm.expect(pm.response.json().errorCode).to.eql(""$code""));" }
    $t
}

$multipagina = '{
  "html": "<html><body><h2>Carta de varias paginas</h2>{{ for i in 1..120 }}<p>Parrafo {{ i }}: el fondo se estampa detras del texto.</p>{{ end }}</body></html>",
  "fileName": "carta-varias-paginas",
  "page": { "size": "A4", "marginMm": 25 },
  "fondo": { "imagen": "{{fondoImagen}}", "paginas": "Primera", "ajuste": "Pagina" }
}'
$sinFondo = $factura -replace ',\s*"fondo": \{[^}]*\}', ''

$folder = [pscustomobject]@{
    name        = '7. Imagen de fondo (HtmlRenderer + PDFsharp)'
    description = "Genera el PDF con HtmlRenderer y despu$([char]0x00E9)s estampa una imagen de Templates/fondo DETR$([char]0x00C1)S del contenido (XGraphicsPdfPageOptions.Prepend), por lo que texto, bordes y rellenos quedan visibles.`n`n" +
                  "- fondo.imagen: nombre sin extensi$([char]0x00F3)n (.png, .jpg o .jpeg). Variable de colecci$([char]0x00F3)n {{fondoImagen}} (por defecto fondo-de-cartas; use fondo para el de prueba).`n" +
                  "- fondo.paginas: Todas (defecto) | Primera`n" +
                  "- fondo.ajuste: Ancho (defecto) | Pagina | Centrado`n" +
                  "- Sin el objeto fondo se usa Pdf:Fondo:Default en todas las p$([char]0x00E1)ginas.`n" +
                  "- Cabeceras de respuesta: X-Pdf-Engine, X-Render-Time-Ms, X-Background, X-Background-Ms."
    item        = @(
        (Request "Factura con fondo (todas las p$([char]0x00E1)ginas, ancho)" (Body '{{fondoImagen}}' 'Todas' 'Ancho') $true 'Imagen al ancho de la hoja, alto proporcional, anclada arriba.' ($ok + $inlineTest)),
        (Request 'Factura con fondo estirado a la hoja' (Body '{{fondoImagen}}' 'Todas' 'Pagina') $true 'ajuste=Pagina: la imagen cubre toda la hoja (puede deformarse).' $ok),
        (Request 'Factura con fondo centrado' (Body '{{fondoImagen}}' 'Todas' 'Centrado') $true "ajuste=Centrado: tama$([char]0x00F1)o natural centrado; se reduce si no cabe." $ok),
        (Request "Varias p$([char]0x00E1)ginas, fondo solo en la primera" $multipagina $true "HTML en l$([char]0x00ED)nea que ocupa varias p$([char]0x00E1)ginas; paginas=Primera (membrete de carta)." $ok),
        (Request 'Sin objeto fondo (usa Pdf:Fondo:Default)' $sinFondo $false "Descarga el PDF con la imagen por defecto en todas las p$([char]0x00E1)ginas." $ok),
        (Request '404 - imagen de fondo inexistente' (Body 'no-existe' 'Todas' 'Ancho') $false 'El fondo se valida antes de generar el PDF.' (Problem 404 'Documents.BackgroundNotFound')),
        (Request "400 - nombre de fondo inv$([char]0x00E1)lido (path traversal)" '{ "template": "factura", "fondo": { "imagen": "../appsettings" } }' $false "Solo letras, n$([char]0x00FA)meros, - y _ ; sin extensi$([char]0x00F3)n." (Problem 400 'Documents.BackgroundNameInvalid')),
        (Request "400 - nombre con espacios o extensi$([char]0x00F3)n" '{ "template": "factura", "fondo": { "imagen": "fondo de cartas.png" } }' $false "Renombre el archivo a fondo-de-cartas.png y env$([char]0x00ED)e fondo-de-cartas." (Problem 400 'Documents.BackgroundNameInvalid')),
        (Request "400 - ajuste no v$([char]0x00E1)lido" '{ "template": "factura", "fondo": { "ajuste": "Mosaico" } }' $false 'Valores: Ancho, Pagina, Centrado.' @('pm.test("HTTP 400", () => pm.response.to.have.status(400));')),
        (Request '404 - plantilla HTML inexistente' '{ "template": "no-existe", "fondo": { "imagen": "{{fondoImagen}}" } }' $false 'El fondo existe pero la plantilla no.' (Problem 404 'Documents.TemplateNotFound'))
    )
}

$j.item = @($j.item | Where-Object { $_.name -notlike '7. Imagen de fondo*' }) + $folder

# Variable de coleccion para elegir la imagen de fondo
$vars = @($j.variable | Where-Object { $_ -and $_.key -ne 'fondoImagen' })
$vars += [pscustomobject]@{ key = 'fondoImagen'; value = 'fondo-de-cartas'; type = 'string'; description = 'Imagen de Templates/fondo sin extension. Use "fondo" para el fondo de prueba.' }
if ($j.PSObject.Properties['variable']) { $j.variable = $vars } else { $j | Add-Member -NotePropertyName variable -NotePropertyValue $vars }

[IO.File]::WriteAllText($file, ($j | ConvertTo-Json -Depth 50), (New-Object System.Text.UTF8Encoding $false))

# Vuelve al formato original (2 espacios) para que el diff solo muestre la carpeta nueva
node (Join-Path $PSScriptRoot 'format-postman.js')
'OK: ' + (($j.item | Select-Object -Last 1).item | ForEach-Object { $_.name }) -join ' | '
