using Aw.Kernel;

namespace Aw.Meta.Core;

/// <summary>The Meta error catalogue (codes AW-META-NNN).</summary>
public static class MetaErrors
{
    /// <summary>AW-META-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-META-001", ErrorKind.Invalid, "Meta input failed validation");

    /// <summary>Every Meta error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
