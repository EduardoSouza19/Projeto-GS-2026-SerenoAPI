using System.Diagnostics;

namespace SerenoAPI.Telemetry;

public static class AppTelemetry
{
    public static readonly ActivitySource ActivitySource = new("SerenoAPI");
}
