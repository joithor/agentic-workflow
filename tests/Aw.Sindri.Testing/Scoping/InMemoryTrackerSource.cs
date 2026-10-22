using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The in-memory <see cref="ITrackerSource"/>: the seeded issues and projects, or no tracker at all.</summary>
public sealed class InMemoryTrackerSource : ITrackerSource
{
    private readonly TrackerSeed? _seed;

    /// <summary>A source over the seeded tracker.</summary>
    /// <param name="seed">What the tracker holds.</param>
    public InMemoryTrackerSource(TrackerSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        _seed = seed;
    }

    private InMemoryTrackerSource()
    {
    }

    /// <summary>A source with no tracker configured.</summary>
    public static InMemoryTrackerSource Unconfigured { get; } = new();

    /// <inheritdoc/>
    public ValueTask<Result<TrackerRecord>> Read(string key, TrackerRead read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(read);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Find(key, read));
    }

    private static List<TrackerComment> Cut(IReadOnlyList<TrackerComment> comments, TrackerRead read) => [.. comments.Take(read.CommentsPerItem)];

    private static TrackerItem Item(TrackerSeedIssue issue, TrackerRead read) => new(issue.Key, issue.Title, issue.Text, Cut(issue.Comments, read));

    private Result<TrackerRecord> Find(string key, TrackerRead read)
    {
        if (_seed is null)
        {
            return ScopingErrors.NoTrackerSource.With("no tracker is configured");
        }

        if (_seed.Issues.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)) is { } issue)
        {
            return new TrackerRecord(issue.Key, TrackerKind.Issue, issue.Title, issue.Text, TrackerSeed.Updated, Cut(issue.Comments, read), [], issue.Comments.Count > read.CommentsPerItem);
        }

        if (_seed.Projects.FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)) is { } project)
        {
            var inside = _seed.Issues.Where(i => string.Equals(i.Project, project.Name, StringComparison.Ordinal)).ToList();
            var cut = inside.Exists(i => i.Comments.Count > read.CommentsPerItem);
            return new TrackerRecord(project.Name, TrackerKind.Project, project.Name, project.Text, TrackerSeed.Updated, [], [.. inside.Take(read.MaxChildren).Select(i => Item(i, read))], inside.Count > read.MaxChildren || cut);
        }

        return SindriErrors.InvalidInput.With($"the tracker has no project or issue '{key}'");
    }
}
