using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Ledger;

/// <summary>
/// An event-type filter (API spec §5.2 'types=', ReadLedger 'type='): a comma list of up to 16 globs, each a dotted
/// type in lower case ('[a-z0-9-]+(\.[a-z0-9-]+)*') with an optional final '.*'.
/// </summary>
public sealed record TypeFilter
{
    private const int MaxGlobs = 16;
    private const string Wildcard = ".*";

    internal TypeFilter(IReadOnlyList<string> globs)
    {
        Globs = globs;
    }

    /// <summary>The globs, in the order given.</summary>
    public IReadOnlyList<string> Globs { get; }

    /// <summary>Parses <paramref name="text"/>; a malformed glob, an empty list or more than 16 globs is SND-CLI-003.</summary>
    /// <param name="text">The comma list.</param>
    /// <returns>The filter.</returns>
    public static Result<TypeFilter> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var globs = text.Split(',');
        if (globs.Length > MaxGlobs)
        {
            return SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"A type filter takes at most {MaxGlobs} globs, not {globs.Length}"));
        }

        foreach (var glob in globs)
        {
            if (!IsGlob(glob))
            {
                return SindriErrors.InvalidInput.With($"'{glob}' isn't an event type or a 'prefix.*' glob (fix: lower case dotted names, like service.* or process.started)");
            }
        }

        return new TypeFilter(globs);
    }

    /// <summary>True when <paramref name="type"/> matches any glob.</summary>
    /// <param name="type">The event type.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        foreach (var glob in Globs)
        {
            if (IsPrefix(glob)
                ? type.StartsWith(glob.AsSpan(0, glob.Length - 1), StringComparison.Ordinal)
                : string.Equals(type, glob, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsPrefix(string glob) => glob.EndsWith(Wildcard, StringComparison.Ordinal);

    private static bool IsGlob(string glob)
    {
        var name = IsPrefix(glob) ? glob[..^Wildcard.Length] : glob;
        var segmentLength = 0;
        foreach (var c in name)
        {
            if (c == '.')
            {
                if (segmentLength == 0)
                {
                    return false;
                }

                segmentLength = 0;
            }
            else if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
            {
                segmentLength++;
            }
            else
            {
                return false;
            }
        }

        return segmentLength > 0;
    }
}
