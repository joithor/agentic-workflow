namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>The capabilities a recipe gained and lost since its last approved hash (setup-once spec §7.2 'delta').</summary>
/// <param name="Added">Capabilities in the new hash that the approved one lacked.</param>
/// <param name="Removed">Capabilities the approved hash had that the new one lacks.</param>
public sealed record CapabilityDelta(IReadOnlyList<CapabilityLine> Added, IReadOnlyList<CapabilityLine> Removed)
{
    /// <summary>No change in capabilities.</summary>
    public static CapabilityDelta None { get; } = new([], []);
}
