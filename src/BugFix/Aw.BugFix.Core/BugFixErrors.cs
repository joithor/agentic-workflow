using Aw.Kernel;

namespace Aw.BugFix.Core;

/// <summary>The BugFix error catalogue (codes AW-BUGFIX-NNN).</summary>
public static class BugFixErrors
{
    /// <summary>AW-BUGFIX-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-BUGFIX-001", ErrorKind.Invalid, "BugFix input failed validation");

    /// <summary>Every BugFix error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
