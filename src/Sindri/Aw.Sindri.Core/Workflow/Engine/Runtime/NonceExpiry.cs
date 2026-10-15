using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The refusal for a nonce past its lifetime (SND-WF-009); shared by every <see cref="ICommandNonces"/>.</summary>
public static class NonceExpiry
{
    /// <summary>
    /// A claim for the same command that never reported applied may still have applied (a lost response), so the refusal
    /// sends the caller to status and events before any rerun; running it again blind could apply it twice.
    /// </summary>
    /// <param name="nonce">The expired nonce.</param>
    /// <param name="sameCommand">Whether the claim is for the command now asking.</param>
    /// <param name="applied">Whether the claim recorded that the command applied.</param>
    /// <returns>The error.</returns>
    public static Error Refusal(CommandNonce nonce, bool sameCommand, bool applied)
    {
        var check = "check `sindri workflow status` and `sindri workflow events` for the instance";
        return WorkflowErrors.NonceRefused.With((sameCommand, applied) switch
        {
            (true, true) => $"nonce {nonce.Value} expired, but its command applied (fix: {check}; nothing needs rerunning)",
            (true, false) => $"nonce {nonce.Value} expired, and its command may have applied (fix: {check} first; run the command again with a new nonce only if it did not)",
            _ => $"nonce {nonce.Value} expired (fix: run the command again; the CLI fetches a new nonce)",
        });
    }
}
