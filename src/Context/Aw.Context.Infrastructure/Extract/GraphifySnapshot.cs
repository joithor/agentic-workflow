using System.Formats.Tar;
using Aw.Context.Core.Engine;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>
/// The private snapshot of a commit that graphify reads: the archive's regular files, with no link, no path that climbs out,
/// nothing in graphify's own folders, no file over the per-file cap, and no path the repo denies (secrets never reach graphify).
/// Only bytes actually copied count toward the snapshot cap; what the caps leave out is counted, never silent.
/// </summary>
internal static class GraphifySnapshot
{
    /// <summary>What <see cref="Unpack"/> left out because of a cap.</summary>
    /// <param name="limits">The per-file and snapshot caps.</param>
    internal sealed class Tally(ClusterLimits limits)
    {
        /// <summary>The per-file and snapshot caps.</summary>
        public ClusterLimits Limits { get; } = limits;

        /// <summary>Files left out because the snapshot cap was reached.</summary>
        public int OverSnapshot { get; set; }

        /// <summary>Files left out because they were over the per-file cap.</summary>
        public int OverFile { get; set; }

        /// <summary>Bytes copied.</summary>
        public long Copied { get; set; }
    }

    /// <summary>Writes the tar's files under the snapshot as described above.</summary>
    /// <param name="tar">The archive.</param>
    /// <param name="snapshot">The folder to write under.</param>
    /// <param name="repo">The repo, for its deny globs.</param>
    /// <param name="tally">The caps, and the count of what they left out.</param>
    /// <param name="cancellationToken">Stops the copy.</param>
    internal static async Task Unpack(Stream tar, string snapshot, ContextRepo repo, Tally tally, CancellationToken cancellationToken)
    {
        var reader = new TarReader(tar, leaveOpen: true);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false) is { } entry)
            {
                if (SafePath(entry) is not { } relative || repo.Denies(relative) || entry.DataStream is null || Skipped(entry.Length, tally))
                {
                    continue;
                }

                var target = Path.Combine(snapshot, relative);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);   // why: target is a file path under the snapshot
                var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using (file.ConfigureAwait(false))
                {
                    await entry.DataStream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                }

                tally.Copied += entry.Length;
            }
        }
    }

    // Whether a file of this size stays out; counts it. Only copied bytes count toward the snapshot cap, so a skipped file never uses up room.
    private static bool Skipped(long length, Tally tally)
    {
        var limits = tally.Limits;
        if (limits.MaxFileBytes is { } perFile && length > perFile)
        {
            tally.OverFile++;
            return true;
        }

        if (limits.MaxSnapshotBytes is { } cap && tally.Copied + length > cap)
        {
            tally.OverSnapshot++;
            return true;
        }

        return false;
    }

    // The entry's path when it is a regular file that stays inside the snapshot and outside graphify's own folders; else null.
    private static string? SafePath(TarEntry entry)
    {
        var segments = entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return entry.EntryType != TarEntryType.RegularFile || segments.Length == 0 || segments.Contains("..") || segments.Contains(".")
            || segments[0].StartsWith("graphify-out", StringComparison.Ordinal) || segments[0].StartsWith(GraphifyClusterSource.PrivateFolder, StringComparison.Ordinal)
            ? null
            : string.Join('/', segments);
    }
}
