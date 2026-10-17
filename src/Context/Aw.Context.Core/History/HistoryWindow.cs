using Aw.Kernel;

namespace Aw.Context.Core.History;

/// <summary>The absolute window of a history read: changes and records since an instant (inclusive), the world as of another (inclusive).</summary>
/// <param name="Since">The earliest instant kept, or null.</param>
/// <param name="Before">The latest instant, or null for today.</param>
public sealed record HistoryWindow(DateTimeOffset? Since, DateTimeOffset? Before)
{
    /// <summary>No window: today's history, nothing cut.</summary>
    public static HistoryWindow Open { get; } = new(Since: null, Before: null);

    /// <summary>Parses the two instants: RFC 3339 with an offset or 'Z', or a date YYYY-MM-DD (UTC midnight). A relative form, an unparseable value or since after before is SND-CONTEXT-006.</summary>
    /// <param name="since">The since text, or null.</param>
    /// <param name="before">The before text, or null.</param>
    /// <returns>The window, or SND-CONTEXT-006.</returns>
    public static Result<HistoryWindow> Of(string? since, string? before) => throw new NotSupportedException();

    /// <summary>Whether <paramref name="at"/> is not after <see cref="Before"/> (the cut is inclusive).</summary>
    /// <param name="at">The instant.</param>
    /// <returns>True when there is no cut or <paramref name="at"/> is at or before it.</returns>
    public bool NotAfterCut(DateTimeOffset at) => throw new NotSupportedException();
}
