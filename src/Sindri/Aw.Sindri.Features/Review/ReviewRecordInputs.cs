using System.Text.Json.Nodes;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Review;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Review;

/// <summary>What the record Step needs from its context: where it runs, the phase packet it reviews and the review loop's facts.</summary>
/// <param name="Address">The instance's address.</param>
/// <param name="Repo">The instance's repo.</param>
/// <param name="Dir">The phase packet's directory.</param>
/// <param name="PacketHash">The phase packet's hash.</param>
/// <param name="Facts">The reviewed head, the running review and the lens log.</param>
internal sealed record ReviewRecordInputs(string Address, RepoName Repo, string Dir, string PacketHash, ReviewFacts Facts)
{
    /// <summary>Reads them; null when the context lacks one.</summary>
    /// <param name="context">The Step's context.</param>
    /// <returns>The inputs.</returns>
    internal static ReviewRecordInputs? Of(StepContext context)
    {
        var packet = StepFacts.Read(context, "fact:phasePacket");
        return context.Address is { } address && context.Repo is { } repo
            && (string?)packet?["dir"] is { } dir && (string?)packet["hash"] is { } hash
            && StepFacts.Head(StepFacts.Read(context, "fact:change")) is { } head && StepFacts.Read(context, "fact:review") is JsonObject review
                ? new ReviewRecordInputs(address, repo, dir, hash, new ReviewFacts(head, review, StepFacts.Read(context, "fact:lensRounds")))
                : null;
    }
}
