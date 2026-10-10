using Aw.Kernel;

namespace Aw.Readiness.Core;

/// <summary>The Readiness error catalogue (codes AW-READINESS-NNN).</summary>
public static class ReadinessErrors
{
    /// <summary>AW-READINESS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-READINESS-001", ErrorKind.Invalid, "Readiness input failed validation");

    /// <summary>Every Readiness error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
