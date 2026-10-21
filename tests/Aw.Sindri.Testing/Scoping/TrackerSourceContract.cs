using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Xunit;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The contract every <see cref="ITrackerSource"/> honours, run against the in-memory source and the file one (scoping spec §4, C8).</summary>
public abstract class TrackerSourceContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static TrackerComment Comment(int number) => new("Rowan", "Comment " + number.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static TrackerSeed Seed() => new(
        [new TrackerSeedProject("Notes app", "A small notes app."), new TrackerSeedProject("Empty", "No issues.")],
        [
            new TrackerSeedIssue("NTS-1", "Roles", "Three roles.", "Notes app", [Comment(1), Comment(2), Comment(3)]),
            new TrackerSeedIssue("NTS-2", "Links", "Share by link.", "Notes app", []),
            new TrackerSeedIssue("NTS-3", "Digest", "A weekly digest.", "Notes app", [Comment(1)]),
            new TrackerSeedIssue("NTS-4", "Loose", string.Empty, Project: null, []),
        ]);

    /// <summary>A source over the seeded tracker.</summary>
    /// <param name="seed">What the tracker holds.</param>
    /// <returns>The source.</returns>
    protected abstract ITrackerSource Create(TrackerSeed seed);

    /// <summary>A source with no tracker configured.</summary>
    /// <returns>The source.</returns>
    protected abstract ITrackerSource CreateUnconfigured();

    /// <summary>An issue reads back with its title, text, version and comments, and no children.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_issue_reads_back_with_its_text_and_comments()
    {
        var read = await Create(Seed()).Read("NTS-2", new TrackerRead(50, 20), Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(TrackerKind.Issue, read.Value.Kind);
        Expect.Equal("NTS-2", read.Value.Key);
        Expect.Equal("Links", read.Value.Title);
        Expect.Equal("Share by link.", read.Value.Text);
        Expect.Equal(TrackerSeed.Updated, read.Value.Version);
        Expect.Equal(0, read.Value.Children.Count);
        Expect.True(!read.Value.Truncated);
    }

    /// <summary>The first comments are kept in order, and a cut marks the record truncated.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Comments_over_the_cap_are_cut_and_the_record_is_truncated()
    {
        var read = await Create(Seed()).Read("NTS-1", new TrackerRead(50, 2), Token);

        Expect.True(read.IsSuccess);
        Expect.Sequence(["Comment 1", "Comment 2"], read.Value.Comments.Select(static c => c.Text));
        Expect.Equal("Rowan", read.Value.Comments[0].Author);
        Expect.True(read.Value.Truncated);
    }

    /// <summary>A project reads back with its issues as children, in order, each with its comments.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_project_reads_back_with_its_issues_as_children()
    {
        var read = await Create(Seed()).Read("Notes app", new TrackerRead(50, 20), Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(TrackerKind.Project, read.Value.Kind);
        Expect.Equal("A small notes app.", read.Value.Text);
        Expect.Sequence(["NTS-1", "NTS-2", "NTS-3"], read.Value.Children.Select(static c => c.Key));
        Expect.Equal(3, read.Value.Children[0].Comments.Count);
        Expect.True(!read.Value.Truncated);
    }

    /// <summary>Children over the cap are cut and the record is truncated.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Children_over_the_cap_are_cut_and_the_record_is_truncated()
    {
        var read = await Create(Seed()).Read("Notes app", new TrackerRead(2, 1), Token);

        Expect.True(read.IsSuccess);
        Expect.Sequence(["NTS-1", "NTS-2"], read.Value.Children.Select(static c => c.Key));
        Expect.Equal(1, read.Value.Children[0].Comments.Count);
        Expect.True(read.Value.Truncated);
    }

    /// <summary>A key that names nothing is invalid input.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_key_that_names_nothing_is_invalid_input()
    {
        var read = await Create(Seed()).Read("NTS-99", TrackerRead.Scoping, Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, read.Error.Code);
    }

    /// <summary>With no tracker configured the read is SND-SCOPING-005.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task No_tracker_configured_is_SND_SCOPING_005()
    {
        var read = await CreateUnconfigured().Read("NTS-1", TrackerRead.Scoping, Token);

        Expect.Equal(ScopingErrors.NoTrackerSource.Code, read.Error.Code);
    }
}
