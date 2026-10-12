namespace Aw.Context.Core.Sources;

/// <summary>
/// The limits a source declares (README X61): CTX D6's "no rate limiting" is a property of local sources, which declare
/// none, not a global rule. Declared only; nothing enforces them yet. Each value, when set, is positive, and
/// <paramref name="Requests"/> and <paramref name="Window"/> are set together.
/// </summary>
/// <param name="Requests">At most this many calls per <paramref name="Window"/>; null for no request limit.</param>
/// <param name="Window">The window <paramref name="Requests"/> counts over; null with it.</param>
/// <param name="MaxConcurrency">At most this many calls in flight at once; null for no limit.</param>
public sealed record SourceLimits(int? Requests = null, TimeSpan? Window = null, int? MaxConcurrency = null)
{
    /// <summary>Whether the declaration is well formed: positive values, and a request count only with its window.</summary>
    public bool IsWellFormed =>
        (Requests is null) == (Window is null)
        && Requests is null or > 0
        && (Window is null || Window.Value > TimeSpan.Zero)
        && MaxConcurrency is null or > 0;
}
