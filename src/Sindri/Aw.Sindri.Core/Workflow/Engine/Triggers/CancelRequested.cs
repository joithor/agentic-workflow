using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// A cancel from any state (spec §3.8): the instance ends with outcome 'cancelled', the stack is dropped, and its
/// children are cancelled. Exempt from the approval check, so every instance can be cancelled under a revoke (W11).
/// </summary>
/// <param name="Actor">The caller, or the service that carried the tracker's or the parent's cancel.</param>
/// <param name="Origin">Where it came from.</param>
/// <param name="Stamp">The nonce and argument digest of a command; null otherwise.</param>
/// <param name="Sequence">The tracker event's ledger sequence, or the parent's version after its cancel; null for a command.</param>
/// <param name="Parent">The cancelled parent, for <see cref="CancelOrigin.Parent"/>.</param>
public sealed record CancelRequested(
    string Actor,
    CancelOrigin Origin,
    CommandStamp? Stamp,
    long? Sequence,
    WorkflowInstanceId? Parent) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.Cancel;

    /// <inheritdoc/>
    public override string KeyPart => Origin switch
    {
        CancelOrigin.Command when Stamp is { } stamp => stamp.KeyPart(TriggerNames.Cancel),
        CancelOrigin.Parent => string.Create(CultureInfo.InvariantCulture, $"cancel:parent:{Parent?.Value}:{Sequence}"),
        CancelOrigin.Command or CancelOrigin.Tracker or _ => string.Create(CultureInfo.InvariantCulture, $"cancel:tracker:{Sequence}"),
    };

    /// <inheritdoc/>
    public override bool ExemptFromApproval => true;
}
