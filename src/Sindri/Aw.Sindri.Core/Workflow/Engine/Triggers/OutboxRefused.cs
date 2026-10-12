using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// An effect's recheck refused it (spec §5.7): 'outbox.refused', written by the effect's activity before it ran. The
/// instance parks with spec-conflict of that kind, unless the same revoke already parked it.
/// </summary>
/// <param name="Actor">'system:dispatcher'.</param>
/// <param name="Effect">The refused effect's identity.</param>
/// <param name="Code">The refusal: SND-RECIPE-005 (revoked), SND-RECIPE-006 (drift) or SND-WF-011 (not allowlisted).</param>
/// <param name="Sequence">The 'outbox.refused' event's ledger sequence.</param>
public sealed record OutboxRefused(string Actor, Decisions.EffectIdentity Effect, string Code, long Sequence) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.OutboxRefused;

    /// <inheritdoc/>
    public override string KeyPart => string.Create(CultureInfo.InvariantCulture, $"outbox.refused:{Sequence}");
}
