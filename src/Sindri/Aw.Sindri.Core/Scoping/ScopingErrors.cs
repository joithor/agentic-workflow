using Aw.Kernel;

namespace Aw.Sindri.Core.Scoping;

/// <summary>
/// The scoping station's error catalogue (scoping spec §8.4), beside <see cref="SindriErrors"/> and validated with it.
/// SND-SCOPING-004 and -005 are the readers' (SC-T) and land with them; -003 is retired in spec rev 3 (a form refusal is the engine's generic code).
/// </summary>
public static class ScopingErrors
{
    /// <summary>SND-SCOPING-001: 'scoping check' or 'scoping show' on an address that is not a workflow.scoping instance.</summary>
    public static readonly ErrorDefinition NotAScopingInstance = new("SND-SCOPING-001", ErrorKind.Invalid, "The instance is not a workflow.scoping instance");

    /// <summary>SND-SCOPING-002: a draft or desk output directory that is outside the run area, or inside a git worktree.</summary>
    public static readonly ErrorDefinition DraftOutsideRunArea = new("SND-SCOPING-002", ErrorKind.Invalid, "Draft files belong under the run area, never in a git worktree");

    /// <summary>SND-SCOPING-006: 'scoping show' on an instance whose run area was purged and whose packet was never frozen.</summary>
    public static readonly ErrorDefinition PacketPurged = new("SND-SCOPING-006", ErrorKind.NotFound, "The scope packet is gone and was never frozen");

    /// <summary>The catalogue's codes.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [NotAScopingInstance, DraftOutsideRunArea, PacketPurged];

    /// <summary>Codes the spec retired (never reused): SND-SCOPING-003.</summary>
    public static IReadOnlyList<string> Retired { get; } = ["SND-SCOPING-003"];
}
