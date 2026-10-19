namespace Aw.Meta.Core.Usage;

/// <summary>What the watch remembers about one limited subject.</summary>
/// <param name="Key">The stop's key.</param>
/// <param name="Since">When the watch first saw the stop.</param>
/// <param name="OkAt">When it last announced the window available; null before that.</param>
/// <param name="Resumed">Whether the resume line was typed (or needs no typing) since then.</param>
internal sealed record UsageEntry(string Key, DateTimeOffset Since, DateTimeOffset? OkAt, bool Resumed);
