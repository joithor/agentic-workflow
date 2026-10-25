using Aw.Kernel;

namespace Aw.Sindri.Core.Scoping;

/// <summary>
/// The scoping station's error catalogue (scoping spec §8.4), beside <see cref="SindriErrors"/> and validated with it.
/// SND-SCOPING-004 is the brief reader's (the desk block reads it in SC-K2, the block runtime in SC-T); -005 is the tracker reader's; -003 is retired in spec rev 3 (a form refusal is the engine's generic code).
/// </summary>
public static class ScopingErrors
{
    /// <summary>SND-SCOPING-001: 'scoping check' or 'scoping show' on an address that is not a workflow.scoping instance.</summary>
    public static readonly ErrorDefinition NotAScopingInstance = new("SND-SCOPING-001", ErrorKind.Invalid, "The instance is not a workflow.scoping instance");

    /// <summary>SND-SCOPING-002: a draft or desk output directory that is outside the run area, or inside a git worktree.</summary>
    public static readonly ErrorDefinition DraftOutsideRunArea = new("SND-SCOPING-002", ErrorKind.Invalid, "Draft files belong under the run area, never in a git worktree");

    /// <summary>SND-SCOPING-004: a brief path outside the repo root and the notes dir, or under a denied glob.</summary>
    public static readonly ErrorDefinition BriefPathRefused = new("SND-SCOPING-004", ErrorKind.Invalid, "The brief path is outside the repo root and the notes dir, or under a denied glob");

    /// <summary>SND-SCOPING-005: 'read-tracker' with no tracker source configured.</summary>
    public static readonly ErrorDefinition NoTrackerSource = new("SND-SCOPING-005", ErrorKind.Invalid, "No tracker source is configured");

    /// <summary>SND-SCOPING-006: 'scoping show' on an instance whose run area was purged and whose packet was never frozen.</summary>
    public static readonly ErrorDefinition PacketPurged = new("SND-SCOPING-006", ErrorKind.NotFound, "The scope packet is gone and was never frozen");

    /// <summary>SND-SCOPING-007: a packet file asked for by a hash the packet directory no longer has (a revise rewrote it); a hash tool failure is SND-SCOPING-008.</summary>
    public static readonly ErrorDefinition PacketChanged = new("SND-SCOPING-007", ErrorKind.Conflict, "The packet changed since it was hashed");

    /// <summary>SND-SCOPING-008: the hash tool (aw-packets) is missing or failed, so a packet file cannot be checked.</summary>
    public static readonly ErrorDefinition PacketToolFailed = new("SND-SCOPING-008", ErrorKind.Unavailable, "The packet hash tool is missing or failed");

    /// <summary>SND-SCOPING-009: the context lenses gather needs were unavailable, after the input's retries. Not a question for a person: the Step is retried, then parks as unavailable (Health shows it) and runs again on `--resume`.</summary>
    public static readonly ErrorDefinition LensesUnavailable = new("SND-SCOPING-009", ErrorKind.Unavailable, "The context lenses were unavailable");

    /// <summary>The catalogue's codes.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [NotAScopingInstance, DraftOutsideRunArea, BriefPathRefused, NoTrackerSource, PacketPurged, PacketChanged, PacketToolFailed, LensesUnavailable];

    /// <summary>Codes the spec retired (never reused): SND-SCOPING-003.</summary>
    public static IReadOnlyList<string> Retired { get; } = ["SND-SCOPING-003"];
}
