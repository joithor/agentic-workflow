using System.Globalization;

namespace Aw.Meta.Core.Usage;

/// <summary>A usage-limit stop found in a pane or a transcript.</summary>
/// <param name="Kind">Which limit.</param>
/// <param name="ResetsAt">When the limit says it resets; null when the text gave no time Claude Code's format covers.</param>
/// <param name="Text">The line that said so.</param>
public sealed record UsageStop(UsageKind Kind, DateTimeOffset? ResetsAt, string Text)
{
    /// <summary>Names the stop: the same limit seen again has the same key, a new one a new key.</summary>
    public string Key => Kind + "@" + (ResetsAt is { } at ? at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : "unknown");

    /// <summary>The reset time as the watch prints it.</summary>
    public string ResetText => ResetsAt is { } at ? at.UtcDateTime.ToString("u", CultureInfo.InvariantCulture) : "unknown";
}
