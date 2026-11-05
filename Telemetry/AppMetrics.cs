using System.Diagnostics.Metrics;

namespace SerenoAPI.Telemetry;

public static class AppMetrics
{
    public static readonly Meter Meter = new("SerenoAPI", "1.0.0");

    public static readonly Counter<long> RequestsCounter =
        Meter.CreateCounter<long>(
            "serenoapi.requests",
            description: "Quantidade de operações executadas pela API.");

    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>(
            "serenoapi.request.duration",
            unit: "ms",
            description: "Duração das operações executadas pela API.");

    public static readonly Counter<long> ReportesCriados =
        Meter.CreateCounter<long>(
            "serenoapi.reportes.criados",
            description: "Quantidade de reportes de equipamentos enviados.");
}
