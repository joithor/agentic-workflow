namespace Aw.CodeIndex.Core.Model;

/// <summary>What the snapshot may keep: the deny globs and the size caps of the settings (spec §5.1, §13).</summary>
/// <param name="DenyPaths">The globs added to the built-in secret globs (never replacing them), case-insensitive.</param>
/// <param name="MaxFileBytes">A larger file is skipped as too-large.</param>
/// <param name="MaxTotalBytes">The sum of kept sizes over this stops the build with SND-CODEIDX-003 before any content is read.</param>
/// <param name="MaxFiles">More kept files than this stops the build (the fixed ceiling in <see cref="CodeIndexRules"/>).</param>
public sealed record SnapshotLimits(IReadOnlyList<string> DenyPaths, long MaxFileBytes, long MaxTotalBytes, int MaxFiles);
