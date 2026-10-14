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

    /// <summary>
    /// Category prefixes never written below debug, whatever <see cref="MinimumLevel"/> says: third-party libraries whose
    /// trace level logs whole messages (the MCP SDK logs every JSON-RPC message at trace, so a question and the records
    /// it returns would reach the log). Default: 'ModelContextProtocol'.
    /// </summary>
    public IReadOnlyList<string> TraceCappedCategories { get; init; } = ["ModelContextProtocol"];

    /// <summary>Where log lines go. Null: standard error.</summary>
    public ILogSink? Sink { get; init; }

    /// <summary>The scrubber on every log and export path. Default: <see cref="Redactor.Default"/>.</summary>
    public IRedactor Redactor { get; init; } = Redaction.Redactor.Default;

    /// <summary>The clock for log timestamps. Default: <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>The activity sources and meters listened to (OpenTelemetry wildcard). Default: 'Aw.*'.</summary>
    public string SourcePattern { get; init; } = "Aw.*";

    /// <summary>The span exporter, an adapter the host opts into. Null (the default): spans are never exported.</summary>
    public BaseExporter<Activity>? TraceExporter { get; init; }

    /// <summary>The metric exporter, an adapter the host opts into. Null (the default): metrics are never collected or exported.</summary>
    public BaseExporter<Metric>? MetricExporter { get; init; }

    /// <summary>
    /// A problem found while reading the environment (an unknown <see cref="LevelVariable"/>), or null. The runtime writes
    /// it once, as a warning line with the error code as its event name, and carries on with the default.
    /// </summary>
    public Error? Problem { get; init; }

    /// <summary>
    /// Options for <paramref name="serviceName"/> with the level read from <see cref="LevelVariable"/> and the host's
    /// clock. An unknown level never fails the host: the level stays information and <see cref="Problem"/> says why,
    /// without echoing the value (it could be anything pasted into the variable).
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <param name="clock">The host's clock (the one Compose receives).</param>
    /// <returns>The options.</returns>
    public static TelemetryOptions FromEnvironment(string serviceName, Func<string, string?> environment, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var options = new TelemetryOptions { ServiceName = serviceName, Clock = clock };
        var value = environment(LevelVariable);
        if (string.IsNullOrEmpty(value))
        {
            return options;
        }

        return _levels.TryGetValue(value, out var level)
            ? options with { MinimumLevel = level }
            : options with
            {
                Problem = TelemetryErrors.InvalidSetting.With(
                    $"{LevelVariable} is not a level (trace, debug, information, warning, error, critical or none); logging at information."),
            };
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
