using Aw.Kernel;

namespace Aw.Packets.Core;

/// <summary>The Packets error catalogue (codes AW-PACKETS-NNN).</summary>
public static class PacketsErrors
{
    /// <summary>AW-PACKETS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-PACKETS-001", ErrorKind.Invalid, "Packets input failed validation");

    /// <summary>Every Packets error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
