namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The capabilities a new hash added and dropped.</summary>
/// <param name="Added">Newly allowed.</param>
/// <param name="Removed">No longer allowed.</param>
internal sealed record CapabilityDelta(IReadOnlyList<CapabilityLine> Added, IReadOnlyList<CapabilityLine> Removed);
