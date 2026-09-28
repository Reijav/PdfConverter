<#
.SYNOPSIS
    Prueba los 6 motores contra una API en ejecucion y guarda los PDFs en ./out.
.EXAMPLE
    ./scripts/smoke-test.ps1 -BaseUrl http://localhost:5080
#>
param([string]$BaseUrl = 'http://localhost:5080')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$samples = Join-Path $root 'src\Danec.PdfGenerator.Api\Templates\samples'
$out = Join-Path $root 'out'
New-Item -ItemType Directory $out -Force | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding $false

$factura = [IO.File]::ReadAllText((Join-Path $samples 'factura.json'), $utf8)
$certificado = [IO.File]::ReadAllText((Join-Path $samples 'certificado.json'), $utf8)
$bodies = @{
    factura     = "{`"template`":`"factura`",`"data`":$factura}"
    certificado = "{`"template`":`"certificado`",`"page`":{`"orientation`":`"Landscape`"},`"data`":$certificado}"
}

$cases = @(
    @{ Engine = 'itext';        Body = 'factura' },
    @{ Engine = 'questpdf';     Body = 'factura' },
    @{ Engine = 'htmlrenderer'; Body = 'factura' },
    @{ Engine = 'puppeteer';    Body = 'factura' },
    @{ Engine = 'playwright';   Body = 'factura' },
    @{ Engine = 'overlay';      Body = 'certificado' }
)

foreach ($case in $cases) {
    $bodyFile = Join-Path $out "$($case.Body).body.json"
    [IO.File]::WriteAllText($bodyFile, $bodies[$case.Body], $utf8)
    $pdf = Join-Path $out "$($case.Engine).pdf"
    $headers = Join-Path $out "$($case.Engine).headers"

    $status = & curl.exe -s --noproxy '*' -o $pdf -D $headers -w '%{http_code}' `
        -H 'Content-Type: application/json' --data-binary "@$bodyFile" "$BaseUrl/api/v1/pdf/$($case.Engine)"

    $render = (Get-Content $headers | Where-Object { $_ -like 'X-Render-Time-Ms*' }) -replace 'X-Render-Time-Ms:\s*', ''
    $size = (Get-Item $pdf).Length
    $isPdf = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($pdf)[0..3]) -eq '%PDF'

    '{0,-13} HTTP {1}  {2,8:N0} bytes  render {3,7} ms  {4}' -f $case.Engine, $status, $size, $render, $(if ($isPdf) { 'OK' } else { Get-Content $pdf -Raw })
}
