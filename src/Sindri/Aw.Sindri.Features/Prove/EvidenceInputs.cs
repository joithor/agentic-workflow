using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Prove;

/// <summary>What the evidence Step needs from its context besides the facts it hands to the rules.</summary>
/// <param name="Address">The instance's address.</param>
/// <param name="Repo">The instance's repo.</param>
/// <param name="Dir">The phase packet's directory.</param>
/// <param name="Head">The built head.</param>
/// <param name="PacketHash">The phase packet's hash.</param>
internal sealed record EvidenceInputs(string Address, RepoName Repo, string Dir, string Head, string PacketHash)
{
    /// <summary>Reads them; null when the context lacks one.</summary>
    /// <param name="context">The Step's context.</param>
    /// <returns>The inputs.</returns>
    internal static EvidenceInputs? Of(StepContext context)
    {
        var packet = StepFacts.Read(context, "fact:phasePacket");
        return context.Address is { } address && context.Repo is { } repo && (string?)packet?["dir"] is { } dir && (string?)packet["hash"] is { } hash && StepFacts.Head(StepFacts.Read(context, "fact:built")) is { } head
            ? new EvidenceInputs(address, repo, dir, head, hash)
            : null;
    }
}
