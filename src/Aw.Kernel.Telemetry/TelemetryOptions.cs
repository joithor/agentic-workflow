using System.Diagnostics;
using Aw.Redaction;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// How a host sets up telemetry. Exporting is off unless the host passes an exporter: <see cref="TraceExporter"/> and
/// <see cref="MetricExporter"/> are null by default, and then nothing leaves the process (spec §5, W7).
/// </summary>
public sealed record TelemetryOptions
{
    /// <summary>The environment variable that sets <see cref="MinimumLevel"/> (trace, debug, information, warning, error, critical, none).</summary>
    public const string LevelVariable = "AW_LOG_LEVEL";

    /// <summary>The service name on spans and metrics, for example 'aw-readiness'.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The lowest level written. Default: information.</summary>
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    /// <summary>Where log lines go. Null: standard error.</summary>
    public ILogSink? Sink { get; init; }

    /// <summary>The scrubber on every log and export path. Default: <see cref="Redactor.Default"/>.</summary>
    public IRedactor Redactor { get; init; } = Aw.Redaction.Redactor.Default;

    /// <summary>The clock for log timestamps. Default: <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>The activity sources and meters listened to (OpenTelemetry wildcard). Default: 'Aw.*'.</summary>
    public string SourcePattern { get; init; } = "Aw.*";

    /// <summary>The span exporter, an adapter the host opts into. Null (the default): spans are never exported.</summary>
    public BaseExporter<Activity>? TraceExporter { get; init; }

    /// <summary>The metric exporter, an adapter the host opts into. Null (the default): metrics are never collected or exported.</summary>
    public BaseExporter<Metric>? MetricExporter { get; init; }

    /// <summary>Options for <paramref name="serviceName"/> with the level read from <see cref="LevelVariable"/>.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs).</param>
    /// <returns>The options, or AW-TELEMETRY-001 when the level isn't one of the names above.</returns>
    public static Result<TelemetryOptions> FromEnvironment(string serviceName, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var value = environment(LevelVariable);
        if (string.IsNullOrEmpty(value))
        {
            return new TelemetryOptions { ServiceName = serviceName };
        }

        return _levels.TryGetValue(value, out var level)
            ? new TelemetryOptions { ServiceName = serviceName, MinimumLevel = level }
            : TelemetryErrors.InvalidSetting.With(
                $"{LevelVariable}='{value}' is not a level: use trace, debug, information, warning, error, critical or none.");
    }

    private static readonly Dictionary<string, LogLevel> _levels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["trace"] = LogLevel.Trace,
        ["debug"] = LogLevel.Debug,
        ["information"] = LogLevel.Information,
        ["warning"] = LogLevel.Warning,
        ["error"] = LogLevel.Error,
        ["critical"] = LogLevel.Critical,
        ["none"] = LogLevel.None,
    };
}
