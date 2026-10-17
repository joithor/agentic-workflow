using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Kernel;

namespace Aw.Context.Testing.History;

/// <summary>An in-memory first-parent history for one repo; <see cref="FileHistoryContract"/> holds it to the git adapter's behavior.</summary>
/// <param name="known">The repo it answers for.</param>
/// <param name="world">Its history.</param>
/// <param name="shallow">Whether its clone is shallow.</param>
public sealed class FakeFileHistory(ContextRepo known, HistoryWorld world, bool shallow = false) : IFileHistory
{
    /// <inheritdoc/>
    public ValueTask<Result<FileHistory>> Changes(ContextRepo repo, FileHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(query);
        return ValueTask.FromResult(Read(repo, query));
    }

    private static bool Clean(string path) => path.Length > 0 && !path.Any(char.IsControl);

    private Result<FileHistory> Read(ContextRepo repo, FileHistoryQuery query)
    {
        var (upTo, paths, window, max) = query;
        if (repo.Name.Value != known.Name.Value || repo.Path != known.Path)
        {
            return ContextErrors.ExtractionUnreadable.With("the fake holds no such repo");
        }

        if (!paths.All(Clean))
        {
            return ContextErrors.QueryInvalid.With("a path holds a control character");
        }

        var from = 0;
        if (upTo is not null)
        {
            if (upTo.Length < 7 || !upTo.All(char.IsAsciiHexDigitLower))
            {
                return ContextErrors.QueryInvalid.With("not a commit sha");
            }

            from = world.Commits.ToList().FindIndex(c => c.Sha.StartsWith(upTo, StringComparison.Ordinal));
            if (from < 0)
            {
                return ContextErrors.CommitUnknown.With("the fake holds no such commit");
            }
        }

        var wanted = paths.ToHashSet(StringComparer.Ordinal);
        var found = world.Commits.Skip(from)
            .Where(c => window.NotAfterCut(c.At) && (window.Since is not { } since || c.At >= since))
            .Select(c => c with { Files = [.. c.Files.Where(wanted.Contains).Order(StringComparer.Ordinal)] })
            .Where(c => c.Files.Count > 0)
            .Take(max + 1)
            .ToList();
        return new FileHistory(world.Head, shallow, [.. found.Take(max)], found.Count > max);
    }
}
