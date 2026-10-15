using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Core.Guard;

/// <summary>
/// Stops a stop-check from blocking forever on an obligation the meta can't or won't close: the identical open set blocks
/// at most 'max' times in a row, and only a stop that the hook says is already a continuation (hook-active) is allowed.
/// </summary>
public static class StopLoopGuard
{
    /// <summary>Names an open set: the same obligations give the same text, in any order.</summary>
    /// <param name="open">The open obligations.</param>
    /// <returns>The fingerprint; empty when nothing is open.</returns>
    public static string Fingerprint(IReadOnlyList<Obligation> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        return string.Join('|', open.Select(o => $"{o.Kind}:{o.Key}").Order(StringComparer.Ordinal));
    }

    /// <summary>Decides one stop-check.</summary>
    /// <param name="previous">The remembered state.</param>
    /// <param name="fingerprint">The current open set's fingerprint.</param>
    /// <param name="hookActive">Whether the stop hook is already continuing a block.</param>
    /// <param name="max">How many identical blocks in a row are allowed before a continuation is let through.</param>
    /// <returns>The decision.</returns>
    public static GuardDecision Decide(GuardState previous, string fingerprint, bool hookActive, int max)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(fingerprint);
        if (fingerprint.Length == 0)
        {
            return new GuardDecision(Block: false, GuardState.None, Allowed: false, FirstAllowed: false);
        }

        var count = string.Equals(previous.Fingerprint, fingerprint, StringComparison.Ordinal) ? previous.Count : 0;
        return hookActive && count >= max
            ? new GuardDecision(Block: false, new GuardState(fingerprint, Math.Max(count, max + 1)), Allowed: true, FirstAllowed: count == max)
            : new GuardDecision(Block: true, new GuardState(fingerprint, count + 1), Allowed: false, FirstAllowed: false);
    }
}
