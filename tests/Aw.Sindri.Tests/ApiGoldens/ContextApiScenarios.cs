using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The context routes' goldens for the app (S-4 UI contract, D11): each answer of the lenses, file map, map review, history,
/// promote, map status and node routes, and each refusal the app maps (003, 037, 044, 045, 046, 047), on a host over the small
/// MapTabsRepo (a fixed sha) with the tabs map imported. The map-tabs draft is one POST map/proposals (a copy
/// of current); the bulk accept runs over the split-panes fixture repo and a rebuild with fake clusters. The author is "golden",
/// so override ids and "who" are the same on every machine.
/// </summary>
internal static class ContextApiScenarios
{
    private const string Repo = MapTabsRepo.Repo;
    private const string Anchor = "feature%3Atabs.tab-manager";

    private const string ThreeKeys = "nodes=feature%3Atabs.tab-manager&nodes=feature%3Atabs.tab-groups&nodes=feature%3Anope.x";

    /// <summary>The POST body for the same three keys as the GET's query.</summary>
    internal const string SubgraphBody = """{"nodes":["surface:command/switch-tab","code:src/tabs/TabManager.ts","decision:d036352234b17baf9a7b2e7cffef599bdc9c8209e92c389d2bcbb919c74c162b","feature:nope.x"]}""";   // a real mixed set: a surface, a code file, a decision, one absent key

    private static string Subgraph(string query) => "/sindri/context/subgraph?" + query;

    private delegate Task<string> Run(Scenario scenario, CancellationToken ct);


    private static readonly (string Slug, string Scenario, Run Body)[] _table =
    [
        ("context-lenses", "map-tabs", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}", ct)),
        ("context-lenses", "depth-1", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&depth=1", ct)),
        ("context-lenses", "unknown-anchor-003", async (s, ct) => await s.Fetch("/sindri/context/lenses?anchor=feature%3Atabs.nope", ct)),
        ("context-lenses", "ambiguous-037", async (s, ct) => await s.Fetch("/sindri/context/lenses?anchor=Layout", ct)),
        ("context-lenses", "no-draft-044", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&draft=true", ct)),
        ("context-lenses", "set-two-anchors", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&anchor=feature%3Atabs.tab-groups", ct)),
        ("context-lenses", "set-missing-anchor", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&anchor=feature%3Anope.x", ct)),
        ("context-lenses", "set-21-anchors-006", async (s, ct) => await s.Fetch("/sindri/context/lenses?" + string.Join('&', Enumerable.Range(0, 21).Select(i => "anchor=feature%3Anope." + i.ToString(System.Globalization.CultureInfo.InvariantCulture))), ct)),
        ("context-lenses", "map-version-v1", async (s, ct) => await s.FetchAfterSecondMap($"/sindri/context/lenses?anchor={Anchor}&mapVersion=v1", ct)),
        ("context-lenses", "map-version-not-retained-120", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&mapVersion=v99", ct)),
        ("context-lenses", "session-set-other-repo", async (s, ct) => await s.FetchAsSession($"/sindri/context/lenses?anchor={Anchor}&anchor=feature%3Aother.thing", ct)),
        ("context-subgraph", "get-three-keys", async (s, ct) => await s.Fetch(Subgraph(ThreeKeys), ct)),
        ("context-subgraph", "post-packet-set", async (s, ct) => await s.PostQuery("/sindri/context/subgraph", SubgraphBody, ct)),
        ("context-subgraph", "depth-2", async (s, ct) => await s.Fetch(Subgraph(ThreeKeys) + "&depth=2", ct)),
        ("context-subgraph", "missing-after-v2", async (s, ct) => await s.FetchAfterSecondMap(Subgraph(ThreeKeys), ct)),
        ("context-subgraph", "session-own-repo", async (s, ct) => await s.FetchAsSession(Subgraph(ThreeKeys) + "&depth=2", ct)),
        ("context-subgraph", "too-many-006", async (s, ct) => await s.Fetch(Subgraph(string.Join('&', Enumerable.Range(0, 21).Select(i => "nodes=feature%3Anope." + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))), ct)),
        ("context-subgraph", "not-retained-120", async (s, ct) => await s.Fetch(Subgraph(ThreeKeys) + "&mapVersion=v99", ct)),
        ("context-subgraph", "post-refused-origin", async (s, ct) => await s.PostQueryForeignOrigin("/sindri/context/subgraph", SubgraphBody, ct)),
        ("context-eval", "map-tabs", async (s, ct) => await s.Fetch("/sindri/context/eval/map", ct)),
        ("context-filemap", "root", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}", ct)),
        ("context-filemap", "src", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}&path=src/", ct)),
        ("context-filemap", "src-tabs", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}&path=src/tabs/", ct)),
        ("context-filemap", "src-unmapped", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}&path=src/tabs/&unmapped=true", ct)),
        ("context-filemap", "repo-outside-045", async (s, ct) => await s.Fetch("/sindri/context/filemap?repo=elsewhere", ct)),
        ("context-filemap", "no-draft-044", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}&draft=true", ct)),
        ("context-map", "map-tabs-anchor", async (s, ct) => await s.Fetch($"/sindri/context/map?anchor={Anchor}", ct)),
        ("context-query", "diff-spl-103", async (s, ct) => await s.Fetch($"/sindri/context/query?diff={DiffRange(s)}&repo={FixtureMap.Repo}", ct)),
        ("context-query", "diff-unknown-sha-110", async (s, ct) => await s.Fetch($"/sindri/context/query?diff={_unknownBase}..{HeadOf(s)}&repo={FixtureMap.Repo}", ct)),
        ("context-node", "tab-manager", async (s, ct) => await s.Fetch($"/sindri/context/node?key={Anchor}", ct)),
        ("context-map-review", "no-draft-044", async (s, ct) => await s.Fetch("/sindri/context/map/review", ct)),
        ("context-map-review", "session-auth-002", async (s, ct) => await s.SessionReview(ct)),
        ("context-map-review", "map-tabs-draft", async (s, ct) => await s.MapTabsDraft(ct)),
        ("context-map-review", "post-accept-link", async (s, ct) => await s.PostAcceptLink(ct)),
        ("context-map-review", "post-undo-list", async (s, ct) => await s.PostUndoList(ct)),
        ("context-map-review", "post-drop", async (s, ct) => await s.PostDrop(ct)),
        ("context-map-review", "post-repoint", async (s, ct) => await s.PostRepoint(ct)),
        ("context-map-review", "stale-047", async (s, ct) => await s.Stale(ct)),
        ("context-map-review", "post-accept-deterministic", async (s, ct) => await s.AcceptDeterministic(ct)),
        ("context-map-review", "post-apply-file-preview", async (s, ct) => await s.ApplyFilePreview(ct)),
        ("context-map-review-history", "after-accept-undo", async (s, ct) => await s.AcceptThenUndo(ct)),
        ("context-map-proposals", "map-tabs-run", async (s, ct) => await s.Propose(ct)),
        ("context-map-promote", "refused-046", async (s, ct) => await s.PromoteRefused(ct)),
        ("context-map-promote", "preview", async (s, ct) => await s.PromotePreview(ct)),
    ];

    /// <summary>Every (slug, scenario) pair of the context goldens.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> Rows { get; } = [.. _table.Select(t => (t.Slug, t.Scenario))];

    /// <summary>Whether the route slug belongs to the context routes.</summary>
    public static bool Handles(string slug) => slug.StartsWith("context-", StringComparison.Ordinal);

    /// <summary>Starts the host the scenario runs on, with the map imported (the bulk accept: a rebuilt draft over the fixture repo).</summary>
    public static async Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct)
    {
        if (slug == "context-query")
        {
            return await StartDiff(ct);
        }

        var bulk = (slug, scenario) is ("context-map-review", "post-accept-deterministic");
        var locks = Directory.CreateTempSubdirectory("aw-golden-locks-");
        IDisposable repo = bulk ? FixtureRepo.Build() : MapTabsRepo.Build();
        var path = repo is FixtureRepo fixture ? fixture.Path : ((MapTabsRepo)repo).Path;
        var host = await ContextHosts.Approved(path, sessions: true, ct, Wiring(Path.Combine(locks.FullName, "heavy-job.lock")), bulk ? "split-panes" : Repo);
        host.Own(repo);
        host.Own(new Deleted(locks.FullName));
        if (bulk)
        {
            await Rebuild(host, ct);
        }
        else
        {
            await ContextMapApiTests.Import(host, ImportFile(host, slug, scenario));
        }

        return host;
    }

    // The diff goldens read PR 3 of the split-panes fixture repo, whose shas are the same on every machine.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GoldenHost, string> _diffRanges = [];

    private static readonly string _unknownBase = new('1', 40);

    private static string HeadOf(Scenario scenario) => DiffRange(scenario)[(DiffRange(scenario).IndexOf("..", StringComparison.Ordinal) + 2)..];

    private static string DiffRange(Scenario scenario) => _diffRanges.TryGetValue(scenario.RunsOn(), out var range) ? range : throw new InvalidOperationException("not a diff scenario");

    private static async Task<GoldenHost> StartDiff(CancellationToken ct)
    {
        var repo = FixtureRepo.Build();
        var host = await ContextHosts.Approved(repo, sessions: true, ct, name: FixtureMap.Repo);
        host.Own(repo);
        await ContextMapApiTests.Import(host);
        var merge = repo.PullRequests.Single(p => p.Number == 3).MergeCommit;
        _diffRanges.Add(host, $"{Rev(repo, merge + "^1")}..{Rev(repo, merge + "^2")}");
        return host;
    }

    private static string Rev(FixtureRepo repo, string rev)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var arg in (string[])["-C", repo.Path, "rev-parse", rev])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = System.Diagnostics.Process.Start(start)!;   // why: git is on the test machine's PATH
        var sha = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return sha;
    }

    /// <summary>Runs the scenario and returns the body to compare (a scenario's setup steps are not part of it).</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        var run = _table.Single(t => t.Slug == slug && t.Scenario == scenario).Body;
        return await run(new Scenario(host), ct);
    }

    private static string ImportFile(GoldenHost host, string slug, string scenario)
    {
        if ((slug, scenario) is not ("context-lenses", "ambiguous-037"))
        {
            return MapTabsRepo.WriteMap(host.Home);
        }

        var file = Path.Combine(host.Home, "ambiguous-map.yaml");
        File.WriteAllText(file, string.Join('\n', "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:layout.a\", label: \"Layout\"}", "  - {key: \"feature:layout.b\", label: \"Layout\"}") + "\n");
        return file;
    }

    private static Action<IServiceCollection> Wiring(string lockPath) => services =>
    {
        _ = services.RemoveAll<IClusterSource>();
        _ = services.AddSingleton<IClusterSource>(new FakeClusterSource());
        _ = services.RemoveAll<ICurrentUser>();
        _ = services.AddSingleton<ICurrentUser>(new FakeCurrentUser("golden"));
        _ = services.RemoveAll<HeavyLockSettings>();
        _ = services.AddSingleton(new HeavyLockSettings(lockPath, 2, Held: false));
    };

    private static async Task Rebuild(GoldenHost host, CancellationToken ct)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "context-rebuild");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        var built = await service.Run(new ScheduledRun(grant, DateTimeOffset.UnixEpoch), ct);
        Assert.True(built.IsSuccess, built.IsError ? built.Error.Message : string.Empty);
    }

    private sealed class Deleted(string path) : IDisposable
    {
        public void Dispose() => Directory.Delete(path, recursive: true);
    }
}
