using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Danec.PdfGenerator.Application.Abstractions;

/// <summary>Trazas y metricas propias; permiten comparar la latencia de cada motor en Grafana/Azure Monitor.</summary>
public static class PdfTelemetry
{
    public const string Name = "Danec.PdfGenerator";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    public static readonly Histogram<double> RenderDuration = Meter.CreateHistogram<double>(
        "pdf.render.duration", unit: "ms", description: "Tiempo de generacion del PDF por motor");

    public static readonly Counter<long> RenderFailures = Meter.CreateCounter<long>(
        "pdf.render.failures", description: "PDFs que fallaron por motor");
}
