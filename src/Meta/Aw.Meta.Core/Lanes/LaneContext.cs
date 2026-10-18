using System.Text.Json.Serialization;
using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Lanes;

/// <summary>One lane's context size.</summary>
/// <param name="Lane">The lane name.</param>
/// <param name="Session">The session id recorded for the lane; null when none.</param>
/// <param name="Transcript">The transcript path; null when it is missing.</param>
/// <param name="Tokens">The context tokens of the last API call; null when unknown.</param>
/// <param name="LastActivity">When the transcript was last written; null without one.</param>
/// <param name="Threshold">The recycle threshold in tokens.</param>
/// <param name="Verdict">What to do.</param>
/// <param name="Reason">Why the verdict is unknown; null otherwise.</param>
public sealed record LaneContext(
    string Lane,
    SessionId? Session,
    string? Transcript,
    long? Tokens,
    DateTimeOffset? LastActivity,
    long Threshold,
    ContextVerdict Verdict,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason = null)
{
    /// <summary>The size as "487k".</summary>
    /// <returns>The compact form, or "?" when unknown.</returns>
    public string Compact() => Tokens is { } tokens ? (tokens / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture) + "k" : "?";
}
