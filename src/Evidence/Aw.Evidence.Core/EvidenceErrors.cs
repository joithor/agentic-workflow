using Aw.Kernel;

namespace Aw.Evidence.Core;

/// <summary>The Evidence error catalogue (codes AW-EVIDENCE-NNN).</summary>
public static class EvidenceErrors
{
    /// <summary>AW-EVIDENCE-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-EVIDENCE-001", ErrorKind.Invalid, "Evidence input failed validation");

    /// <summary>Every Evidence error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
