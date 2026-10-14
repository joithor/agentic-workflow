namespace Aw.Context.Core.Map;

/// <summary>What the purge removes (spec §6.2, Phase 3): exports after 14 days, retained versions beyond the newest 20.</summary>
public static class MapRetention
{
    /// <summary>How long an export is kept.</summary>
    public static TimeSpan ExportLifetime { get; } = TimeSpan.FromDays(14);

    /// <summary>How many retained versions are kept.</summary>
    public const int RetainedVersions = 20;

    /// <summary>The exports past their lifetime.</summary>
    /// <param name="files">The exports folder's files.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The expired files.</returns>
    public static IReadOnlyList<ExportedFile> ExpiredExports(IReadOnlyList<ExportedFile> files, DateTimeOffset now) =>
        [.. files.Where(f => now - f.Written > ExportLifetime)];

    /// <summary>The retained versions beyond the newest <see cref="RetainedVersions"/>.</summary>
    /// <param name="versions">Every version.</param>
    /// <returns>The versions to delete.</returns>
    public static IReadOnlyList<int> ExcessVersions(IReadOnlyList<MapVersion> versions) =>
        [.. versions.Where(v => v.Status == MapVersionStatus.Retained).OrderByDescending(v => v.Number).Skip(RetainedVersions).Select(v => v.Number)];
}
