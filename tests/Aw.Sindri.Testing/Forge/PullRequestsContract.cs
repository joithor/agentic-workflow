using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Xunit;

namespace Aw.Sindri.Testing.Forge;

/// <summary>
/// The contract every <see cref="IPullRequests"/> honours once a forge is configured: a pull request opened through the port reads back, Find answers none
/// before Open and the pull request after, a marker keeps two pull requests apart, a host outside the list is refused before anything is sent, and a missing
/// pull request is SND-FORGE-003. Run against the fake and, over the file forge, the real adapter.
/// </summary>
public abstract class PullRequestsContract
{
    private const string Feature = "feature/contract";

    /// <summary>A port, with a repository on an allowed host and a host that is not allowed.</summary>
    /// <returns>The port ready for a run.</returns>
    protected abstract PullRequestsUnderTest Create();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A pull request opened through the port reads back open, at its head, with no checks and no threads.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_opened_pull_request_reads_back()
    {
        var (run, opened) = await Open();

        var view = Ok(await run.Pulls.Get(opened.Link, Ct), "Get");

        Expect.Equal(opened.Link, view.Link);
        Expect.Equal(PullState.Open, view.State);
        Expect.Equal(Sha('a'), view.HeadSha);
        Expect.Equal(PullChecksState.None, view.Checks.State);
        Expect.Equal(0, view.UnresolvedThreads);
    }

    /// <summary>Find answers a success with nothing before a pull request is opened, and the pull request after.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Find_answers_none_then_the_opened_pull_request()
    {
        var run = Create();
        var query = new FindPull(run.Repo, Feature, "run-1");

        var before = Ok(await run.Pulls.Find(query, Ct), "Find before");
        var opened = Ok(await run.Pulls.Open(Request(run, marker: "run-1"), Ct), "Open");
        var after = Ok(await run.Pulls.Find(query, Ct), "Find after");

        Expect.True(before is null);
        Expect.Equal(opened, after);
        Expect.True(opened.Number >= 1);
    }

    /// <summary>A different marker or head finds nothing, so a retried effect never takes another run's pull request.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Find_keeps_markers_and_heads_apart()
    {
        var (run, opened) = await Open(marker: "run-1");

        var otherMarker = Ok(await run.Pulls.Find(new FindPull(run.Repo, Feature, "run-2"), Ct), "Find by another marker");
        var otherHead = Ok(await run.Pulls.Find(new FindPull(run.Repo, "feature/other", Marker: null), Ct), "Find by another head");
        var byHead = Ok(await run.Pulls.Find(new FindPull(run.Repo, Feature, Marker: null), Ct), "Find by head alone");

        Expect.True(otherMarker is null && otherHead is null);
        Expect.Equal(opened, byHead);
    }

    /// <summary>Update succeeds on a pull request and answers SND-FORGE-003 for one that isn't there.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Update_changes_a_pull_request_and_refuses_a_missing_one()
    {
        var (run, opened) = await Open();

        _ = Ok(await run.Pulls.Update(opened.Link, "New title", body: null, Ct), "Update");
        var missing = await run.Pulls.Update(MissingLink(run), "New title", body: null, Ct);

        Expect.Equal("SND-FORGE-003", Failure(missing));
    }

    /// <summary>A pull request that isn't there is SND-FORGE-003 on every read.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_pull_request_is_not_found()
    {
        var run = Create();

        Expect.Equal("SND-FORGE-003", Failure(await run.Pulls.Get(MissingLink(run), Ct)));
        Expect.Equal("SND-FORGE-003", Failure(await run.Pulls.CheckLog(MissingLink(run), "build", Ct)));
        Expect.Equal("SND-FORGE-003", Failure(await run.Pulls.RerunChecks(MissingLink(run), Sha('a'), Ct)));
    }

    /// <summary>A host outside the list is SND-FORGE-002 on a link, a find and an open, and nothing is created.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_host_outside_the_list_is_refused()
    {
        var run = Create();
        var foreign = new PullRepo(run.ForeignHost, run.Repo.Owner, run.Repo.Name);

        Expect.Equal("SND-FORGE-002", Failure(await run.Pulls.Get($"https://{run.ForeignHost}/{run.Repo.Owner}/{run.Repo.Name}/pull/1", Ct)));
        Expect.Equal("SND-FORGE-002", Failure(await run.Pulls.Find(new FindPull(foreign, Feature, Marker: null), Ct)));
        Expect.Equal("SND-FORGE-002", Failure(await run.Pulls.Open(Request(run) with { Repo = foreign }, Ct)));
        Expect.Equal("SND-FORGE-002", Failure(await run.Pulls.Get("not a link", Ct)));
    }

    private static string Sha(char c) => new(c, 40);

    private static OpenPull Request(PullRequestsUnderTest run, string? marker = null) =>
        new(run.Repo, Feature, "main", Sha('a'), "Title", "Body", Draft: false, marker);

    private static string MissingLink(PullRequestsUnderTest run) => $"https://{run.Repo.Host}/{run.Repo.Owner}/{run.Repo.Name}/pull/424242";

    private static T Ok<T>(Result<T> result, string what) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"contract: {what} failed with {result.Error.Code}: {result.Error.Message}");

    private static string Failure<T>(Result<T> result) =>
        result.IsError ? result.Error.Code : throw new ContractViolationException("contract: expected an error");

    private async Task<(PullRequestsUnderTest Run, OpenedPull Opened)> Open(string? marker = null)
    {
        var run = Create();
        return (run, Ok(await run.Pulls.Open(Request(run, marker), Ct), "Open"));
    }
}
