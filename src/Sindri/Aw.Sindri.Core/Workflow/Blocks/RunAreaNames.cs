using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>The names a run area accepts: one to three '/'-separated segments of letters, digits, '.', '_' and '-', none starting with '.', so a name can never climb out of the instance's directory.</summary>
public static partial class RunAreaNames
{
    private const int MaxLength = 128;

    /// <summary>Whether <paramref name="name"/> is a valid run-area file name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True when valid.</returns>
    public static bool IsValid(string? name) => name is { Length: > 0 and <= MaxLength } && Name.IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9_][A-Za-z0-9_-]*(\\.[A-Za-z0-9_-]+)*(/[A-Za-z0-9_][A-Za-z0-9_-]*(\\.[A-Za-z0-9_-]+)*){0,2}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Name { get; }
}
