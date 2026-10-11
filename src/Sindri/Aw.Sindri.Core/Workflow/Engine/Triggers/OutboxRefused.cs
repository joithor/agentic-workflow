using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// Dispatch refused one of the instance's outbox commands (spec §5.4): 'outbox.refused', routed only by causation and
/// never expired. The instance parks with spec-conflict of that kind, unless the same revoke already parked it.
/// </summary>
/// <param name="Actor">'system:dispatcher'.</param>
/// <param name="Command">The refused command.</param>
/// <param name="Code">The refusal: SND-RECIPE-005 (revoked), SND-RECIPE-006 (drift) or SND-WF-011 (not allowlisted).</param>
/// <param name="Sequence">The 'outbox.refused' event's ledger sequence.</param>
public sealed record OutboxRefused(string Actor, OutboxCommandId Command, string Code, long Sequence) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.OutboxRefused;

    /// <inheritdoc/>
    public override string KeyPart => string.Create(CultureInfo.InvariantCulture, $"outbox.refused:{Sequence}");
}
