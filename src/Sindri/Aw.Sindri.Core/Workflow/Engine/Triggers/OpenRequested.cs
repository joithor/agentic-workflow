namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// Reserved for P5 (UI spec E-6, X18): an 'item.open-requested' applied directly in Commit wherever a takeover is
/// allowed, keyed 'wf:{instance}:open:{launch}'. It is a trigger, not a command (no nonce), exempt from the approval
/// check like a takeover, with actor 'service:engine' and the request event as its causation; a no-op when the
/// instance is already taken over.
/// </summary>
/// <param name="Actor">'service:engine'.</param>
/// <param name="Launch">The launch request's id ('LCH-…').</param>
public sealed record OpenRequested(string Actor, string Launch) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.OpenRequest;

    /// <inheritdoc/>
    public override string KeyPart => "open:" + Launch;

    /// <inheritdoc/>
    public override bool ExemptFromApproval => true;
}
