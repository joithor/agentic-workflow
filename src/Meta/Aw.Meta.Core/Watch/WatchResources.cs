namespace Aw.Meta.Core.Watch;

/// <summary>The disk and memory states a watch last reported, kept so a re-armed watch does not announce an unchanged one again.</summary>
/// <param name="DiskLow">Whether disk was reported low.</param>
/// <param name="MemoryLow">Whether memory was reported low.</param>
public sealed record WatchResources(bool DiskLow, bool MemoryLow)
{
    /// <summary>Nothing reported: both states are OK.</summary>
    public static WatchResources None { get; } = new(DiskLow: false, MemoryLow: false);

    /// <summary>Reads the kept form.</summary>
    /// <param name="text">The stored text, or null when none was kept.</param>
    /// <returns>The states; <see cref="None"/> for null or anything unreadable.</returns>
    public static WatchResources Parse(string? text) => text is null ? None : new(text.Contains("disk=low", StringComparison.Ordinal), text.Contains("mem=low", StringComparison.Ordinal));

    /// <summary>The kept form: "disk=low mem=ok".</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"disk={(DiskLow ? "low" : "ok")} mem={(MemoryLow ? "low" : "ok")}";
}
