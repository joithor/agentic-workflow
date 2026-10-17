using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The snapshot over the shared /proc-shaped fixture tree and Claude's fixture records (collector spec CO-2a): the real adapters and the real services, no network, no clock.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class SnapshotDiscoveryTests
{
    private const string Aider = """{ "schema": 1, "agents": { "aider": { "label": "Aider", "bin": "aider", "open": ["{bin}", "{seed}"], "sessionId": "none", "processName": "aider" } } }""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<SessionSnapshot> Discover(FakeTimeProvider clock)
    {
        using var fleet = new TempFleet();
        var catalog = new AgentCatalog(LauncherConfigFile.Read(SessionFixtures.Builtins(), Encoding.UTF8.GetBytes(Aider)).Value.Agents);
        var providers = catalog.All.Select(static a => a.Id).ToList();
        var discovery = new SessionDiscovery(new ProcProcessTable(SessionFixtures.Proc, SessionFixtures.OwnUid), new ClaudeSessionRecords(SessionFixtures.ClaudeSessions),
            new FleetStateFiles(fleet.Root, providers, pruning: false), catalog, SessionSettings.Defaults);
        var repos = new SessionRepos(new FakeRepoLocator(), new InMemoryProfiles(), clock);
        var observer = new SessionObserver(new TranscriptFiles("/nonexistent-fixture-root/home"), new PassThroughScrubber(), repos);
        var snapshot = new SessionSnapshotStore();
        var service = new CollectSessionsService(discovery, observer, new CollectorMemory(), snapshot, clock);

        var response = await service.Handle(new CollectSessionsRequest(TimeSpan.FromSeconds(30)), Token);

        Assert.True(response.Value.Good);
        return snapshot.Current!;
    }

    private static string Json(SessionSnapshot snapshot)
    {
        var golden = new GoldenSnapshot([.. snapshot.Sessions.Select(Row)], [.. snapshot.RecentlyEnded.Select(static e => Row(e.Session))],
            [.. snapshot.Problems.Select(static p => new GoldenProblem(p.Source, p.Code, p.Text))]);
        return Encoding.UTF8.GetString(AwJson.Serialize(golden, AwJson.CreateOptions(SessionGoldenJsonContext.Default, indented: true))) + "\n";
    }

    private static GoldenSession Row(ObservedSession s) => new(s.Ref.ToString(), s.State.ToString(), s.Source.ToString(), s.IdKnown, s.Estimated, s.Pid, s.Title, s.Cwd,
        s.LaunchId?.Value, s.Team, s.Lane, s.Since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

    [Fact, Trait("Lane", "integration")]
    public async Task The_snapshot_over_the_fixture_tree_equals_the_golden()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);

        var snapshot = await Discover(clock);

        Golden.Match(Json(snapshot), typeof(SnapshotDiscoveryTests).Assembly, "Sessions/Golden/snapshot-discovery.json", Normalization.None);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_fixture_snapshot_holds_neither_the_planted_token_nor_a_command_line_prompt()
    {
        var snapshot = await Discover(new FakeTimeProvider(KnownInstants.Epoch));

        var text = Json(snapshot);

        Assert.DoesNotContain(ProcessTableContract.PlantedToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt text", text, StringComparison.Ordinal);
        Assert.DoesNotContain("9999", text, StringComparison.Ordinal);   // the stale record with no process never shows
        Assert.DoesNotContain("pid-701", text, StringComparison.Ordinal);   // another program named agent
        Assert.DoesNotContain("pid-212", text, StringComparison.Ordinal);   // claude -p under judge
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Key_files_next_to_the_records_never_become_sessions()
    {
        var snapshot = await Discover(new FakeTimeProvider(KnownInstants.Epoch));

        Assert.DoesNotContain(snapshot.Sessions, static s => s.Ref.Id.Value.Contains("0123abcd", StringComparison.Ordinal));
    }
}
