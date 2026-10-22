using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.BridgeEvents;

/// <summary>
/// The PR watcher (PR babysitter spec §6.2, API15) over the real ledger and the fake forge: one comment gives one 'pr.changed review', unchanged polls give none, a quiet pull request gives
/// one 'idle' per quiet period, a refused credential or an unreachable host degrades the service with the forge's code, and a host that did not answer is left alone for a while.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class PrWatchServiceTests : IDisposable
{
    private const string Item = "T-61";
    private static readonly PullRepo _repo = new("forge.test", "acme", "widgets");
    private readonly WorkflowsHarness _harness = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakePullRequests _forge = new(["forge.test"]);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task One_comment_gives_one_pr_changed_review_for_the_item_with_the_link_as_subject()
    {
        await using var rig = await Rig();
        var pull = _forge.Add(_repo, "feature", "a1");
        rig.Watching(pull.Link);
        _ = await rig.Poll();

        _ = _forge.SeedThread(pull.Link, "please rename this");
        var ran = await rig.Poll();

        Assert.Equal(ScheduledResult.Worked, ran.Value);
        var recorded = Assert.Single(await rig.Events());
        Assert.Equal((Item, "review"), (recorded.Item, Outcome(recorded)));
        var fact = JsonDocument.Parse((await rig.Contents.Read(Fact(recorded), Token)).Value.ToArray()).RootElement;
        Assert.Equal((pull.Link, "a1"), (fact.GetProperty("subject").GetString(), fact.GetProperty("headSha").GetString()));
        Assert.Equal(["review"], fact.GetProperty("changes").EnumerateArray().Select(c => c.GetString()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Ten_unchanged_polls_give_none_and_are_quiet()
    {
        await using var rig = await Rig();
        var pull = _forge.Add(_repo, "feature", "a1");
        rig.Watching(pull.Link);

        var runs = new List<Result<ScheduledResult>>();
        for (var i = 0; i < 10; i++)
        {
            runs.Add(await rig.Poll());
        }

        Assert.All(runs, r => Assert.Equal(ScheduledResult.Quiet, r.Value));
        Assert.Empty(await rig.Events());
    }

    [Fact]
    public async Task A_quiet_pull_request_gives_one_idle_per_quiet_period()
    {
        await using var rig = await Rig();
        var pull = _forge.Add(_repo, "feature", "a1");
        rig.Watching(pull.Link);
        _ = await rig.Poll();

        _clock.Advance(TimeSpan.FromSeconds(61));
        _ = await rig.Poll();
        _ = await rig.Poll();   // the same period: no second idle

        var recorded = Assert.Single(await rig.Events());
        Assert.Equal("idle", Outcome(recorded));

        _clock.Advance(TimeSpan.FromSeconds(61));
        _ = await rig.Poll();
        Assert.Equal(2, (await rig.Events()).Count);   // the next period
    }

    [Fact]
    public async Task A_merge_with_a_new_head_is_one_event_whose_outcome_is_merged_and_whose_changes_list_both()
    {
        await using var rig = await Rig();
        var pull = _forge.Add(_repo, "feature", "a1");
        rig.Watching(pull.Link);
        _ = await rig.Poll();

        pull.State = PullState.Merged;
        pull.HeadSha = "b2";
        _ = await rig.Poll();

        var recorded = Assert.Single(await rig.Events());
        Assert.Equal("merged", Outcome(recorded));
        var fact = JsonDocument.Parse((await rig.Contents.Read(Fact(recorded), Token)).Value.ToArray()).RootElement;
        Assert.Equal(["merged", "updated"], fact.GetProperty("changes").EnumerateArray().Select(c => c.GetString()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_refused_credential_degrades_the_service_with_the_forges_code_and_no_event()
    {
        await using var rig = await Rig();
        rig.Watching(_forge.Add(_repo, "feature", "a1").Link);
        _forge.Failure = SindriErrors.ForgeRefused.With("the forge refused the credential");

        var ran = await rig.Poll();

        Assert.Equal(SindriErrors.ForgeRefused.Code, ran.Error.Code);
        Assert.Empty(await rig.Events());
    }

    [Fact]
    public async Task No_forge_configured_is_SND_FORGE_001()
    {
        await using var rig = await Rig();
        rig.Watching(_forge.Add(_repo, "feature", "a1").Link);
        _forge.Failure = SindriErrors.NoForge.With("no forge");

        Assert.Equal("SND-FORGE-001", (await rig.Poll()).Error.Code);
    }

    [Fact]
    public async Task An_unreachable_host_is_quiet_twice_and_SND_FORGE_004_the_third_poll()
    {
        await using var rig = await Rig();
        rig.Watching(_forge.Add(_repo, "feature", "a1").Link);
        _forge.Failure = SindriErrors.ForgeUnavailable.With("unreachable");

        var first = await rig.Poll(wait: TimeSpan.FromMinutes(1));
        var second = await rig.Poll(wait: TimeSpan.FromMinutes(1));
        var third = await rig.Poll(wait: TimeSpan.FromMinutes(1));

        Assert.Equal((ScheduledResult.Quiet, ScheduledResult.Quiet, SindriErrors.ForgeUnavailable.Code), (first.Value, second.Value, third.Error.Code));
    }

    [Fact]
    public async Task A_host_that_did_not_answer_is_left_alone_for_a_wait_that_doubles_with_each_unanswered_poll()
    {
        await using var rig = await Rig();
        rig.Watching(_forge.Add(_repo, "feature", "a1").Link);
        _forge.Failure = SindriErrors.ForgeUnavailable.With("rate limited");   // the poll interval is 5 s; each poll below starts 6 s after the last unless it says otherwise

        _ = await rig.Poll();                                  // t=6: the first unanswered poll: asked again after 5 s
        _ = await rig.Poll();                                  // t=12: asked; the second: asked again after 10 s
        var asked = _forge.Calls.Count(static c => c == "Digest");
        _ = await rig.Poll();                                  // t=18: the poll interval has passed, the back-off (until t=22) has not
        var during = _forge.Calls.Count(static c => c == "Digest");
        _ = await rig.Poll(wait: TimeSpan.FromSeconds(5));     // t=23: the back-off is over
        var after = _forge.Calls.Count(static c => c == "Digest");

        Assert.Equal((2, 2, 3), (asked, during, after));
    }

    [Fact]
    public async Task The_state_file_round_trips_and_is_owner_only()
    {
        await using var rig = await Rig();
        var written = new PrWatchFile(
            KnownInstants.Epoch,
            new Dictionary<string, HostBackoff>(StringComparer.Ordinal) { ["forge.test"] = new(2, KnownInstants.Epoch.AddMinutes(1)) },
            new Dictionary<string, WatchedRecord>(StringComparer.Ordinal) { ["https://forge.test/acme/widgets/pull/1"] = new(new WatchedDigest(PullState.Open, "a1", PullChecksState.Passing, 3, "v"), KnownInstants.Epoch, IdleAt: null) });

        _ = await rig.Store.Write(written, Token);
        var read = await rig.Store.Read(Token);

        Assert.Equal(written.PolledAt, read.Value.PolledAt);
        Assert.Equal(written.Hosts["forge.test"], read.Value.Hosts["forge.test"]);
        Assert.Equal(written.Pulls.Values.Single(), read.Value.Pulls.Values.Single());
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(rig.File) & (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite));
        }
    }

    [Theory]
    [InlineData("\"https://forge.test/acme/widgets/pull/1\"", "https://forge.test/acme/widgets/pull/1")]
    [InlineData("{\"url\":\"https://forge.test/acme/widgets/pull/2\"}", "https://forge.test/acme/widgets/pull/2")]
    [InlineData("{\"link\":\" https://forge.test/acme/widgets/pull/3 \"}", "https://forge.test/acme/widgets/pull/3")]
    [InlineData("{}", null)]
    [InlineData("not json", null)]
    public void The_link_is_read_from_a_string_or_an_object_fact(string fact, string? expected) =>
        Assert.Equal(expected, TemporalWatchedPulls.LinkOf(System.Text.Encoding.UTF8.GetBytes(fact)));

    private static string? Outcome(LedgerEvent recorded)
    {
        using var payload = JsonDocument.Parse(recorded.Envelope.Payload);
        return payload.RootElement.GetProperty("outcome").GetString();
    }

    private static FactValue Fact(LedgerEvent recorded)
    {
        using var payload = JsonDocument.Parse(recorded.Envelope.Payload);
        var fact = payload.RootElement.GetProperty("facts").GetProperty("fact:prEvent");
        return new FactValue(fact.GetProperty("ref").GetString()!, fact.GetProperty("sha256").GetString()!, HeadSha: null);   // why: the event holds what it was written with
    }

    private async Task<WatchRig> Rig()
    {
        var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        var home = sindri.GetRequiredService<WorkflowsHome>();
        var store = new PrWatchStore(home);
        var watching = new List<WatchedPull>();
        var profile = new FixedProfile(new SindriProfile(1, "thor", Workflow: new WorkflowSettings(Pr: new PrSettings(PollInterval: "5s", RemindAfter: "1m"))));
        var recorder = new PrChangedRecorder(sindri.GetRequiredService<ILedgerStore>(), sindri.GetRequiredService<EventFactory>(), sindri.GetRequiredService<IFactStore>(), NullLogger<PrChangedRecorder>.Instance);
        var service = new PrWatchService(new FixedWatched(watching), _forge, recorder, store, profile, _clock, NullLogger<PrWatchService>.Instance);
        return new WatchRig(sindri, service, store, watching, _clock, home.PrWatchFile);
    }

    private sealed class WatchRig(ServiceProvider sindri, PrWatchService service, PrWatchStore store, List<WatchedPull> watching, FakeTimeProvider clock, string file) : IAsyncDisposable
    {
        public PrWatchStore Store { get; } = store;

        public string File { get; } = file;

        public IFactContents Contents { get; } = sindri.GetRequiredService<IFactContents>();

        public void Watching(string link) => watching.Add(new WatchedPull(Item, link));

        /// <summary>Waits <paramref name="wait"/> (a little over the poll interval by default), then runs the service once.</summary>
        public async ValueTask<Result<ScheduledResult>> Poll(TimeSpan? wait = null)
        {
            clock.Advance(wait ?? TimeSpan.FromSeconds(6));
            return await service.Run(new ScheduledRun(sindri.GetRequiredService<HeldLease>().Current!, clock.GetUtcNow()), Token);
        }

        public async Task<IReadOnlyList<LedgerEvent>> Events() =>
            [.. (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, Token)).Value.Where(e => string.Equals(e.Envelope.Type, "pr.changed", StringComparison.Ordinal))];

        public ValueTask DisposeAsync() => sindri.DisposeAsync();
    }

    private sealed class FixedWatched(List<WatchedPull> pulls) : IWatchedPulls
    {
        public ValueTask<Result<IReadOnlyList<WatchedPull>>> List(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<WatchedPull>>>(pulls.ToList());
    }

    private sealed class FixedProfile(SindriProfile profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<LoadedProfile?>>(new LoadedProfile(profile, ProfileHash.From(new string('a', 64)), "/home/profile.yaml"));
    }
}
