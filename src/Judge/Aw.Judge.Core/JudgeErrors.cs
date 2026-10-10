using Aw.Kernel;

namespace Aw.Judge.Core;

/// <summary>The Judge error catalogue (codes AW-JUDGE-NNN).</summary>
public static class JudgeErrors
{
    /// <summary>AW-JUDGE-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-JUDGE-001", ErrorKind.Invalid, "Judge input failed validation");

    /// <summary>Every Judge error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
