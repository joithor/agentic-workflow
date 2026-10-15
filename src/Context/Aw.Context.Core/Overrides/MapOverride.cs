using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Overrides;

/// <summary>One human decision about the map (spec §6.1, table map_override). It names its target by key, so it survives rebuilds, imports and key migrations (through aliases).</summary>
/// <param name="Action">What it does.</param>
/// <param name="Target">The node it applies to (for add, the new node's key).</param>
/// <param name="Who">The OS user at the terminal who made it.</param>
/// <param name="At">When it was made; an import keeps the file's value.</param>
/// <param name="Label">The new label (rename, add), or null.</param>
/// <param name="Other">The node to merge into or move under (merge, move; for add, its parent feature), or null.</param>
/// <param name="Rejection">For reject: the rejected proposal's <see cref="RejectionIdentity"/>; null rejects whatever the target is.</param>
/// <param name="Link">For accept or reject of an edge (a suggested file link): its id; the target is then the edge's 'from' node. Null for a node decision. Omitted from JSON when null.</param>
/// <param name="Undoes">For undo: the override it reverses. Omitted from JSON when null.</param>
/// <param name="Proposal">For propose: the proposal's run, repo, kind, parent and evidence. Omitted from JSON when null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MapOverride(
    OverrideAction Action,
    NodeKey Target,
    string Who,
    DateTimeOffset At,
    string? Label = null,
    NodeKey? Other = null,
    string? Rejection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LinkId? Link = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OverrideId? Undoes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProposalPayload? Proposal = null)
{
    /// <summary>The id: a hash of the content, so the same decision imported twice is one row (a method: it is never read from a file).</summary>
    /// <returns>The id.</returns>
    public OverrideId ToId()
    {
        var text = $"{Action}\n{Target.Value}\n{Who}\n{At.ToUniversalTime():O}\n{Label}\n{Other?.Value}\n{Rejection}";
        text += ReviewText();
        return OverrideId.From("ovr_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16]);
    }

    // Only the S-4 fields join the hash, and only when set, so every override written before them keeps its id.
    private string ReviewText() =>
        Link is null && Undoes is null && Proposal is null
            ? string.Empty
            : $"\n{Link?.Value}\n{Undoes?.Value}\n{Proposal?.ToHashText()}";
}
