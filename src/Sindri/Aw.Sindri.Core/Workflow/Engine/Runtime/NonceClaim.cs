namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What <see cref="ICommandNonces.Claim"/> found for a nonce (engine spec rev 8 §5.2).</summary>
public enum NonceClaim
{
    /// <summary>This call created the claim: nothing was sent with the nonce yet, and this call may give it back.</summary>
    Fresh = 0,

    /// <summary>The same command already holds the claim but isn't recorded as applied (an earlier send may have applied): resend with the same Update id and let Temporal return the first outcome; never give the claim back.</summary>
    Pending = 1,

    /// <summary>The same command was already applied with this nonce.</summary>
    Applied = 2,
}
