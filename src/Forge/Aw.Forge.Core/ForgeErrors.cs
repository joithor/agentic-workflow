using Aw.Kernel;

namespace Aw.Forge.Core;

/// <summary>The Forge error catalogue (codes AW-FORGE-NNN).</summary>
public static class ForgeErrors
{
    /// <summary>AW-FORGE-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-FORGE-001", ErrorKind.Invalid, "Forge input failed validation");

    /// <summary>Every Forge error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
