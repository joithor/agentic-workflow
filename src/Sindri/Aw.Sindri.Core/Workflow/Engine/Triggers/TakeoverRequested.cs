namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>'workflow takeover' (class pause): pushes a taken-over frame, pausing the deadlines (W19). Allowed on a parked or escalated instance too; exempt from the approval check.</summary>
/// <param name="Actor">The caller (a session may take over).</param>
/// <param name="Stamp">The nonce and argument digest.</param>
public sealed record TakeoverRequested(string Actor, CommandStamp Stamp) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.Takeover;

    /// <inheritdoc/>
    public override string KeyPart => Stamp.KeyPart(TriggerNames.Takeover);

    /// <inheritdoc/>
    public override bool ExemptFromApproval => true;
}
