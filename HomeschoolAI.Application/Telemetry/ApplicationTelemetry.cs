using System.Diagnostics;

namespace HomeschoolAI.Application.Telemetry;

/// <summary>
/// Fuente de actividad compartida para la observabilidad (OpenTelemetry).
/// Los spans se crean con <see cref="ActivitySource"/> y el SDK de OpenTelemetry los exporta.
/// </summary>
public static class ApplicationTelemetry
{
    public const string SourceName = "HomeschoolAI";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
