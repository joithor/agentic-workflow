using Aw.Kernel;

namespace Aw.Sindri.Core;

/// <summary>The Sindri error catalogue (codes AW-SINDRI-NNN).</summary>
public static class SindriErrors
{
    /// <summary>AW-SINDRI-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-SINDRI-001", ErrorKind.Invalid, "Sindri input failed validation");

    /// <summary>Every Sindri error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
