using System.Globalization;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The one-line summaries the map jobs record, written and read through this type so the link 'jobs show' draws from a held refresh to the publish that
/// finished it (code index spec 6.4) can't drift from the words the jobs write.
/// </summary>
internal static class MapJobSummaries
{
    private const string HeldPrefix = "held: map v";
    private const string PromotedMark = " promoted · ";

    /// <summary>A refresh that ended with its map held for the code index.</summary>
    /// <param name="version">The held map's number.</param>
    /// <returns>The summary.</returns>
    internal static string Held(int version) => string.Create(CultureInfo.InvariantCulture, $"{HeldPrefix}{version} waits for the code index");

    /// <summary>A publish that promoted the held map.</summary>
    /// <param name="version">The map's number.</param>
    /// <param name="reason">The decision's reason.</param>
    /// <returns>The summary.</returns>
    internal static string Promoted(int version, string reason) => string.Create(CultureInfo.InvariantCulture, $"map v{version}{PromotedMark}{reason}");

    /// <summary>The held map's number in a refresh's summary, or null when the summary is not a held one.</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>The number, or null.</returns>
    internal static int? HeldVersion(string? summary)
    {
        if (summary is null || !summary.StartsWith(HeldPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var digits = new string([.. summary[HeldPrefix.Length..].TakeWhile(char.IsAsciiDigit)]);
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var version) ? version : null;
    }

    /// <summary>True when a publish job's <paramref name="summary"/> is about map <paramref name="version"/>.</summary>
    /// <param name="summary">The summary.</param>
    /// <param name="version">The map's number.</param>
    /// <returns>Whether it names that map.</returns>
    internal static bool Mentions(string? summary, int version) => summary is not null && summary.Contains(string.Create(CultureInfo.InvariantCulture, $"map v{version} "), StringComparison.Ordinal);

    /// <summary>True when a publish job's <paramref name="summary"/> says the map was promoted.</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>Whether it did.</returns>
    internal static bool IsPromotion(string? summary) => summary is not null && summary.Contains(PromotedMark, StringComparison.Ordinal);
}
