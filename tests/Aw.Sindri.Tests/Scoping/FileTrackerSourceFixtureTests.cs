using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Infrastructure.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The file tracker source over the scoping fixture's tracker export (tests/fixtures/scoping/tracker).</summary>
[Lane(TestLane.Integration)]
public sealed class FileTrackerSourceFixtureTests
{
    internal static string Export() => Path.Combine(RepoRoot(), "tests", "fixtures", "scoping", "tracker");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("the repo root is not above the test binaries");
    }

    [Fact]
    public async Task The_expiry_issue_reads_from_the_export()
    {
        var read = await new FileTrackerSource(Export()).Read("NTS-135", TrackerRead.Scoping, TestContext.Current.CancellationToken);

        Assert.True(read.IsSuccess);
        Assert.Equal((TrackerKind.Issue, "NTS-135"), (read.Value.Kind, read.Value.Key));
        Assert.False(string.IsNullOrWhiteSpace(read.Value.Title));
    }

    [Fact]
    public async Task The_project_reads_its_twenty_five_issues_and_a_cap_of_ten_truncates()
    {
        var source = new FileTrackerSource(Export());

        var whole = await source.Read("Notes app", TrackerRead.Scoping, TestContext.Current.CancellationToken);
        var cut = await source.Read("Notes app", new TrackerRead(10, 20), TestContext.Current.CancellationToken);

        Assert.Equal((25, false), (whole.Value.Children.Count, whole.Value.Truncated));
        Assert.Equal((10, true), (cut.Value.Children.Count, cut.Value.Truncated));
    }
}
