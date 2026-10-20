using System.Text.RegularExpressions;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Launch.Argv;

/// <summary>Which session ids are usable (launcher L-9): the generic bound (<see cref="SessionId"/> itself), then the entry's own pattern.</summary>
public static class SessionIdRules
{
    /// <summary>Non-backtracking matching is linear, so this only backstops a pathological pattern; a tight wall-clock bound made a valid id fail on a loaded box (the first match builds the automaton).</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Regex?> _compiled = new(StringComparer.Ordinal);

    /// <summary>The id when <paramref name="text"/> passes the generic bound (1 to 128 of [A-Za-z0-9._:-], no leading dot, no '..'), else null.</summary>
    /// <param name="text">Text that may be a session id.</param>
    /// <returns>The typed id, or null.</returns>
    public static SessionId? Parse(string? text) => text is not null && SessionId.TryFrom(text, out var id) ? id : null;

    /// <summary>Whether <paramref name="id"/> is present and matches the entry's pattern (run NonBacktracking with a 2 s timeout).</summary>
    /// <param name="entry">The entry whose 'sessionIdPattern' applies; a null pattern accepts any bounded id.</param>
    /// <param name="id">The session id.</param>
    /// <returns>True when the id may be written, expanded into a template or resumed. A pattern that is invalid or unsupported by the non-backtracking engine matches nothing; one that runs over 2 s is not usable either, but <see cref="Check(AgentEntry, SessionId?)"/> tells the two apart.</returns>
    public static bool IsUsable(AgentEntry entry, SessionId? id) => Check(entry, id) == PatternOutcome.Matched;

    /// <summary>How the entry's pattern judges <paramref name="id"/>.</summary>
    /// <param name="entry">The entry; a null pattern accepts any bounded id.</param>
    /// <param name="id">The session id; null is NoMatch.</param>
    /// <returns>Matched, NoMatch, or TimedOut when the match ran past its limit and nothing is known about the id.</returns>
    public static PatternOutcome Check(AgentEntry entry, SessionId? id) => Check(entry, id, DefaultTimeout);

    /// <summary>Whether <paramref name="text"/> is wholly matched by an entry's session id pattern (anchored at both ends; a pattern that doesn't compile, or runs over 2 s, matches nothing: <see cref="Test(string, string)"/> tells a timeout apart).</summary>
    /// <param name="pattern">The entry's pattern.</param>
    /// <param name="text">The id.</param>
    /// <returns>True when the whole text matches.</returns>
    public static bool Matches(string pattern, string text) => Test(pattern, text) == PatternOutcome.Matched;

    /// <summary>How an entry's session id pattern judges <paramref name="text"/> (anchored at both ends).</summary>
    /// <param name="pattern">The entry's pattern.</param>
    /// <param name="text">The id.</param>
    /// <returns>Matched, NoMatch (also for a pattern that doesn't compile: it matches nothing), or TimedOut.</returns>
    public static PatternOutcome Test(string pattern, string text) => Test(pattern, text, DefaultTimeout);

    internal static PatternOutcome Check(AgentEntry entry, SessionId? id, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (id is not { } value)
        {
            return PatternOutcome.NoMatch;
        }

        return entry.SessionIdPattern is not { } pattern ? PatternOutcome.Matched : Test(pattern, value.Value, timeout);
    }

    internal static PatternOutcome Test(string pattern, string text, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(text);
        if (Compiled(pattern, timeout) is not { } regex)
        {
            return PatternOutcome.NoMatch;
        }

        try
        {
            return regex.IsMatch(text) ? PatternOutcome.Matched : PatternOutcome.NoMatch;
        }
        catch (RegexMatchTimeoutException)
        {
            return PatternOutcome.TimedOut;
        }
    }

    private static Regex? Compiled(string pattern, TimeSpan timeout) =>
        timeout == DefaultTimeout ? _compiled.GetOrAdd(pattern, static p => Build(p, DefaultTimeout)) : Build(pattern, timeout);

    private static Regex? Build(string pattern, TimeSpan timeout)
    {
        try
        {
            return new Regex(@"\A(?:" + pattern + @")\z", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, timeout);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
