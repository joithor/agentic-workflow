using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// An effect's stable identity (spec §5.4): '{instance}:{entry}:{index}' with the entered frame's entry, separate
/// from its command id, so a retry or a re-emission is harmless to the effect's existence check. 'effect.claim.ref'
/// keys its identity by item instead (P6).
/// </summary>
/// <param name="Instance">The instance.</param>
/// <param name="Entry">The entry of the frame the transition enters.</param>
/// <param name="Index">The effect's index in the transition's effects.</param>
public sealed record EffectIdentity(WorkflowInstanceId Instance, int Entry, int Index)
{
    /// <summary>'{instance}:{entry}:{index}'.</summary>
    public string Text => string.Create(CultureInfo.InvariantCulture, $"{Instance.Value}:{Entry}:{Index}");
}
