namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>'workflow handback' (class desk; a session can't hand back): pops the taken-over frame back to whatever it covered. Exempt from the approval check.</summary>
/// <param name="Actor">The caller.</param>
/// <param name="Stamp">The nonce and argument digest.</param>
public sealed record HandbackRequested(string Actor, CommandStamp Stamp) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.Handback;

    /// <inheritdoc/>
    public override string KeyPart => Stamp.KeyPart(TriggerNames.Handback);

    /// <inheritdoc/>
    public override bool ExemptFromApproval => true;
}
