using System.Text.RegularExpressions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;

namespace Aw.Sindri.Core.Sessions;

/// <summary>The fallback that finds a session id from the files the CLI wrote (launcher spec §4, 'Fallback'). Pure; 'session wrap' lists the files.</summary>
public static class SessionIdCapture
{
    /// <summary>Picks the one id among the candidate files.</summary>
    /// <param name="rule">The launch's capture rule.</param>
    /// <param name="paths">The candidate files: created after the start, within the wait.</param>
    /// <param name="claimed">Ids another launch already holds; they are not candidates.</param>
    /// <returns>The id when exactly one candidate remains; Ambiguous for more than one; None otherwise.</returns>
    public static SessionIdScan Choose(CaptureRule rule, IReadOnlyList<string> paths, IReadOnlySet<string> claimed) =>
        Choose(rule, paths, claimed, SessionIdRules.DefaultTimeout);

    internal static SessionIdScan Choose(CaptureRule rule, IReadOnlyList<string> paths, IReadOnlySet<string> claimed, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(claimed);
        if (Compile(rule.Capture, timeout) is not { } capture)
        {
            return SessionIdScan.None;
        }

        var timedOut = false;
        var candidates = new List<SessionId>();
        foreach (var path in paths)
        {
            var (captured, captureTimedOut) = Capture(capture, path);
            timedOut |= captureTimedOut;
            if (captured is not { } id || claimed.Contains(id.Value) || candidates.Contains(id))
            {
                continue;
            }

            switch (Check(rule.SessionIdPattern, id, timeout))
            {
                case PatternOutcome.Matched:
                    candidates.Add(id);
                    break;
                case PatternOutcome.TimedOut:
                    timedOut = true;
                    break;
                case PatternOutcome.NoMatch:
                default:
                    break;
            }
        }

        return candidates.Count switch
        {
            0 => new SessionIdScan(Id: null, Ambiguous: false, timedOut),
            1 => new SessionIdScan(candidates[0], Ambiguous: false, timedOut),
            _ => new SessionIdScan(Id: null, Ambiguous: true, timedOut),
        };
    }

    /// <summary>Whether an entry's session id pattern accepts <paramref name="id"/> (no pattern accepts any).</summary>
    /// <param name="pattern">The entry's pattern, or null.</param>
    /// <param name="id">The id.</param>
    /// <returns>True when it matches (run non-backtracking with a 2 s timeout); a pattern that doesn't compile or times out accepts nothing, and <see cref="Check(string, SessionId)"/> tells the two apart.</returns>
    public static bool Accepts(string? pattern, SessionId id) => Check(pattern, id) == PatternOutcome.Matched;

    /// <summary>How an entry's session id pattern judges <paramref name="id"/> (no pattern matches any).</summary>
    /// <param name="pattern">The entry's pattern, or null.</param>
    /// <param name="id">The id.</param>
    /// <returns>Matched, NoMatch or TimedOut.</returns>
    public static PatternOutcome Check(string? pattern, SessionId id) => Check(pattern, id, SessionIdRules.DefaultTimeout);

    internal static PatternOutcome Check(string? pattern, SessionId id, TimeSpan timeout) =>
        pattern is null ? PatternOutcome.Matched : SessionIdRules.Test(pattern, id.Value, timeout);

    private static Regex? Compile(string? pattern, TimeSpan timeout)
    {
        if (pattern is null)
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, timeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static (SessionId? Id, bool TimedOut) Capture(Regex capture, string path)
    {
        try
        {
            var match = capture.Match(path);
            return (match.Success && match.Groups.Count > 1 && SessionId.TryFrom(match.Groups[1].Value, out var id) ? id : null, TimedOut: false);
        }
        catch (RegexMatchTimeoutException)
        {
            return (Id: null, TimedOut: true);
        }
    }
}
