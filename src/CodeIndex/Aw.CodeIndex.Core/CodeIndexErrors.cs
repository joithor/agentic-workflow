using Aw.Kernel;

namespace Aw.CodeIndex.Core;

/// <summary>The CodeIndex error catalogue (codes AW-CODEIDX-NNN).</summary>
public static class CodeIndexErrors
{
    /// <summary>AW-CODEIDX-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-CODEIDX-001", ErrorKind.Invalid, "CodeIndex input failed validation");

    /// <summary>Every CodeIndex error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
