using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.History;

/// <summary>The absolute window of a history read: changes and records since an instant (inclusive), the world as of another (inclusive).</summary>
/// <param name="Since">The earliest instant kept, or null.</param>
/// <param name="Before">The latest instant, or null for today.</param>
public sealed partial record HistoryWindow(DateTimeOffset? Since, DateTimeOffset? Before)
{
    private static readonly string[] _instantFormats = ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];

    /// <summary>No window: today's history, nothing cut.</summary>
    public static HistoryWindow Open { get; } = new(Since: null, Before: null);

    /// <summary>Parses the two instants: RFC 3339 with an offset or 'Z', or a date YYYY-MM-DD (UTC midnight). A relative form, an unparseable value or since after before is SND-CONTEXT-006.</summary>
    /// <param name="since">The since text, or null.</param>
    /// <param name="before">The before text, or null.</param>
    /// <returns>The window, or SND-CONTEXT-006.</returns>
    public static Result<HistoryWindow> Of(string? since, string? before)
    {
        DateTimeOffset? from = null;
        DateTimeOffset? upTo = null;
        if (since is not null)
        {
            if (!TryInstant(since, out var parsed))
            {
                return Invalid("since");
            }

            from = parsed;
        }

        if (before is not null)
        {
            if (!TryInstant(before, out var parsed))
            {
                return Invalid("before");
            }

            upTo = parsed;
        }

        return from > upTo
            ? ContextErrors.QueryInvalid.With("since is after before (fix: swap them, or drop one)")
            : new HistoryWindow(from, upTo);
    }

    /// <summary>Whether <paramref name="at"/> is not after <see cref="Before"/> (the cut is inclusive).</summary>
    /// <param name="at">The instant.</param>
    /// <returns>True when there is no cut or <paramref name="at"/> is at or before it.</returns>
    public bool NotAfterCut(DateTimeOffset at) => Before is not { } cut || at <= cut;

    /// <summary>Reads an RFC 3339 instant with an offset, or a date (UTC midnight). Nothing else: no relative form, no zone-less time.</summary>
    internal static bool TryInstant(string text, out DateTimeOffset instant)
    {
        instant = default;
        return InstantShape.IsMatch(text)
            ? DateTimeOffset.TryParseExact(text, _instantFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out instant)
            : DateShape.IsMatch(text) && DateTimeOffset.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out instant);
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InstantShape { get; }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DateShape { get; }

    private static Error Invalid(string name) =>
        ContextErrors.QueryInvalid.With($"{name} isn't an RFC 3339 instant ('2025-09-01T00:00:00Z') or a date ('2025-09-01'); relative times such as '90d' aren't accepted (fix: pass an absolute time)");
}
