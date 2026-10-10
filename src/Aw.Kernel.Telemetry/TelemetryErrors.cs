namespace Aw.Kernel.Telemetry;

/// <summary>The Aw.Kernel.Telemetry error catalogue (codes AW-TELEMETRY-NNN).</summary>
public static class TelemetryErrors
{
    /// <summary>AW-TELEMETRY-001: a telemetry setting is invalid.</summary>
    public static readonly ErrorDefinition InvalidSetting =
        new("AW-TELEMETRY-001", ErrorKind.Invalid, "A telemetry setting is invalid");

    /// <summary>Every telemetry error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [InvalidSetting];
}
