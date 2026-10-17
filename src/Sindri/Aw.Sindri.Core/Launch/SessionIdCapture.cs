namespace Aw.Sindri.Core.Launch;

/// <summary>The fallback that finds a session id from the files the CLI wrote (launcher spec §4, 'Fallback'). Pure; 'session wrap' lists the files.</summary>
public static class SessionIdCapture
{
    /// <summary>Picks the one id among the candidate files.</summary>
    /// <param name="rule">The launch's capture rule.</param>
    /// <param name="paths">The candidate files: created after the start, within the wait.</param>
    /// <param name="claimed">Ids another launch already holds; they are not candidates.</param>
    /// <returns>The id when exactly one candidate remains; Ambiguous for more than one; None otherwise.</returns>
    public static SessionIdScan Choose(CaptureRule rule, IReadOnlyList<string> paths, IReadOnlySet<string> claimed) =>
        throw new NotSupportedException("SessionIdCapture: make SessionIdCaptureTests pass");

    /// <summary>Whether an entry's session id pattern accepts <paramref name="id"/> (no pattern accepts any).</summary>
    /// <param name="pattern">The entry's pattern, or null.</param>
    /// <param name="id">The id.</param>
    /// <returns>True when it matches (run non-backtracking with a 100 ms timeout).</returns>
    public static bool Accepts(string? pattern, Sessions.SessionId id) =>
        throw new NotSupportedException("SessionIdCapture: make SessionIdCaptureTests pass");
}
