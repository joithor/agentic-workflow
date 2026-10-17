using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// A map version a caller names ('mapVersion=' / '--map-version', scoping spec C10): N, vN or vN@commit, the label an answer prints.
/// </summary>
/// <param name="Number">The version number.</param>
/// <param name="Commit">The commit part, or null when none was given.</param>
public readonly record struct MapVersionRef(int Number, string? Commit)
{
    /// <summary>Parses what a caller sent.</summary>
    /// <param name="text">'12', 'v12' or 'v12@a41b07d'.</param>
    /// <returns>The reference, or SND-CONTEXT-006 naming the value.</returns>
    public static Result<MapVersionRef> Parse(string? text)
    {
        var rest = (text ?? string.Empty).AsSpan();
        if (rest.Length > 0 && rest[0] is 'v' or 'V')
        {
            rest = rest[1..];
        }

        var at = rest.IndexOf('@');
        var number = Number_(at < 0 ? rest : rest[..at]);
        var commit = at < 0 ? null : Commit_(rest[(at + 1)..]);
        return number is { } n && (at < 0 || commit is not null)
            ? new MapVersionRef(n, commit)
            : ContextErrors.QueryInvalid.With($"the map version '{Shown(text)}' isn't N, vN or vN@commit (fix: use the label an answer printed, e.g. v3)");
    }

    /// <summary>How an answer prints the version: 'v12' or 'v12@a41b07d'.</summary>
    public string Label => Commit is null ? Tag(Number) : Tag(Number) + "@" + Commit;

    /// <summary>The version without a commit: 'v12'.</summary>
    /// <param name="number">The version number.</param>
    /// <returns>The tag.</returns>
    public static string Tag(int number) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"v{number}");

    private static int? Number_(ReadOnlySpan<char> digits) =>
        digits.Length is > 0 and <= 9 && digits.IndexOfAnyExceptInRange('0', '9') < 0 && int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) is > 0 and var n ? n : null;

    private static string? Commit_(ReadOnlySpan<char> commit) =>
        commit.Length is >= 4 and <= 64 && IsHex(commit) ? commit.ToString().ToLowerInvariant() : null;

    private static bool IsHex(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static string Shown(string? text) => text is null ? string.Empty : text.Length <= 40 ? text : text[..40] + "...";

    /// <summary>Whether <paramref name="version"/> is the one named: the same number and, when a commit part was given, a commit that starts with it.</summary>
    /// <param name="version">A stored version.</param>
    /// <returns>True when it is the one named.</returns>
    public bool Names(MapVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return version.Number == Number
            && (Commit is null || (version.Commit is { } stored && stored.StartsWith(Commit, StringComparison.OrdinalIgnoreCase)));
    }
}
