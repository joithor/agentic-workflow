using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Host.Commands;

/// <summary>The compact context line of health and status: "context: api 487k (recycle), engine 120k".</summary>
internal static class ContextLine
{
    internal static IEnumerable<string> Of(IReadOnlyList<LaneContext>? lanes)
    {
        var known = lanes?.Where(static l => l.Verdict != ContextVerdict.Unknown).ToList();
        if (known is { Count: > 0 })
        {
            yield return "context: " + string.Join(", ", known.Select(static l => $"{l.Lane} {l.Compact()}" + (l.Verdict == ContextVerdict.Recycle ? " (recycle)" : string.Empty)));
        }
    }
}
