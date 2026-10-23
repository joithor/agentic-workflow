using System.Collections.Concurrent;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Why a launch's session was ended for a refused report (a payload that isn't JSON, twice): the check that ends the session records the text, and the watch that
/// sees the exit reads it, so the failed Step names the refusal instead of a bare 'exit'. In memory, one line per launch, bounded; a restart loses it and the Step
/// fails as a plain exit, never wrongly.
/// </summary>
public sealed class ReportRefusals
{
    /// <summary>The most characters of a refusal that reach a failure.</summary>
    public const int MaxLength = 300;

    private readonly ConcurrentDictionary<LaunchId, string> _byLaunch = [];

    /// <summary>Remembers why <paramref name="launch"/>'s session was ended; the text is made one line and cut to <see cref="MaxLength"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="text">The refusal.</param>
    public void Record(LaunchId launch, string text) => _byLaunch[launch] = OneLine(text);

    private static string OneLine(string text)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= MaxLength ? line : line[..(MaxLength - 1)] + "…";
    }

    /// <summary>The recorded refusal.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The one-line refusal, or null when the session did not end for one.</returns>
    public string? For(LaunchId launch) => _byLaunch.TryGetValue(launch, out var text) ? text : null;
}
