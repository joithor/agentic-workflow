using Aw.Kernel;

namespace Aw.Readiness.Core;

/// <summary>The Readiness error catalogue (codes AW-READINESS-NNN).</summary>
public static class ReadinessErrors
{
    /// <summary>AW-READINESS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-READINESS-001", ErrorKind.Invalid, "Readiness input failed validation");

    /// <summary>AW-READINESS-002: the repo root is missing or not a readable directory.</summary>
    public static readonly ErrorDefinition RootNotFound = new("AW-READINESS-002", ErrorKind.NotFound, "Repo root not found");

    /// <summary>AW-READINESS-003: readiness.yaml is missing or malformed.</summary>
    public static readonly ErrorDefinition ConfigInvalid = new("AW-READINESS-003", ErrorKind.Invalid, "readiness.yaml is missing or malformed");

    /// <summary>AW-READINESS-004: the run history can't be read or written.</summary>
    public static readonly ErrorDefinition HistoryUnavailable = new("AW-READINESS-004", ErrorKind.Unavailable, "Readiness history unavailable");

    /// <summary>Every Readiness error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid, RootNotFound, ConfigInvalid, HistoryUnavailable];
}
