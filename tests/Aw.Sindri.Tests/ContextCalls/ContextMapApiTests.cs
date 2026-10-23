using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The map routes (S-3a): GET /sindri/context/map, GET /sindri/context/map/export, POST /sindri/context/map/import (protected).</summary>
public sealed class ContextMapApiTests
{
    private static string Body(string path, string? hash = null, (string Challenge, string Typed)? answer = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["path"] = path,
            ["hash"] = hash,
            ["answer"] = answer is { } a ? new Dictionary<string, string>(StringComparer.Ordinal) { ["challenge"] = a.Challenge, ["typed"] = a.Typed } : null,
        });

    /// <summary>The preview's content hash and the challenge it issued.</summary>
    private static (string Hash, string Challenge, string Action) Issued(string previewBody)
    {
        var root = JsonDocument.Parse(previewBody).RootElement;
        var challenge = root.GetProperty("challenge");
        return (root.GetProperty("preview").GetProperty("hash").GetString() ?? string.Empty, challenge.GetProperty("challenge").GetString() ?? string.Empty,
            challenge.GetProperty("action").GetString() ?? string.Empty);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Without_a_map_status_is_SND_CONTEXT_030()
    {
        await using var host = await Approved(sessions: false, TestContext.Current.CancellationToken);

        var status = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-030"), (status.Status, status.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Without_a_map_the_publish_status_is_SND_CONTEXT_030()
    {
        await using var host = await Approved(sessions: false, TestContext.Current.CancellationToken);

        var status = await host.Http.Ask(Get("/sindri/context/map/publish", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-030"), (status.Status, status.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task After_an_import_the_publish_status_is_published_by_nobodys_hand_with_the_maps_sentence()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);
        var file = FixtureMap.WriteTo(host.Home);
        var (hash, challenge, action) = Issued(await PreviewBody(host, file));
        _ = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, (challenge, action)), Human(host.Token.Value)));

        var status = await host.Http.Ask(Get("/sindri/context/map/publish", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, status.Status);
        var root = JsonDocument.Parse(status.Body).RootElement;
        Assert.Equal(("published", false, 1), (root.GetProperty("state").GetString(), root.GetProperty("stale").GetBoolean(), root.GetProperty("version").GetInt32()));
        Assert.Equal("Map v1 is current.", root.GetProperty("lines")[0].GetString());
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_human_at_a_terminal_previews_then_imports_by_answering_the_challenge_and_the_import_is_recorded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);
        var file = FixtureMap.WriteTo(host.Home);

        var preview = await host.Http.Ask(Post("/sindri/context/map/import", Body(file), Human(host.Token.Value)));
        var (hash, challenge, action) = Issued(preview.Body);
        var unanswered = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash), Human(host.Token.Value)));
        var imported = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, (challenge, action)), Human(host.Token.Value)));
        var status = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapImported, ct);

        Assert.Equal(HttpStatusCode.OK, preview.Status);
        Assert.Equal("import map " + hash[..6], action);
        Assert.NotEmpty(challenge);
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (unanswered.Status, unanswered.Code));
        Assert.Equal(HttpStatusCode.OK, imported.Status);
        Assert.Contains("\"status\":\"current\"", imported.Body, StringComparison.Ordinal);
        Assert.Contains("\"nodes\":20", status.Body, StringComparison.Ordinal);
        Assert.Contains(hash, recorded.Value!.Envelope.Payload, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_mistyped_answer_is_SND_AUTH_002_and_a_changed_file_is_SND_CONTEXT_034()
    {
        await using var host = await Approved(sessions: false, TestContext.Current.CancellationToken);
        var file = FixtureMap.WriteTo(host.Home);

        var (hash, challenge, action) = Issued(await PreviewBody(host, file));
        var mistyped = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, (challenge, "import map 000000")), Human(host.Token.Value)));
        var afterMistype = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, (challenge, action)), Human(host.Token.Value)));
        var fresh = Issued(await PreviewBody(host, file));
        await File.AppendAllTextAsync(file, "# edited\n", TestContext.Current.CancellationToken);
        var changed = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, (fresh.Challenge, fresh.Action)), Human(host.Token.Value)));
        var status = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (mistyped.Status, mistyped.Code));
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (afterMistype.Status, afterMistype.Code));
        Assert.Equal("SND-CONTEXT-034", changed.Code);
        Assert.Equal("SND-CONTEXT-030", status.Code);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_challenge_is_spent_by_a_successful_import_and_is_bound_to_the_file_it_was_issued_for()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);
        var file = FixtureMap.WriteTo(host.Home);
        var ledger = host.App.Services.GetRequiredService<ILedgerStore>();

        var (hash, challenge, action) = Issued(await PreviewBody(host, file));
        var first = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, (challenge, action)), Human(host.Token.Value)));
        var recorded = await ledger.Latest(EventTypes.ContextMapImported, ct);
        var replay = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, (challenge, action)), Human(host.Token.Value)));
        var afterReplay = await ledger.Latest(EventTypes.ContextMapImported, ct);

        var other = Path.Combine(host.Home, "other-map.yaml");
        await File.WriteAllTextAsync(other, (await File.ReadAllTextAsync(file, ct)).Replace("label: Split pane", "label: Split panes", StringComparison.Ordinal), ct);
        var issuedForFile = Issued(await PreviewBody(host, file));
        var issuedForOther = Issued(await PreviewBody(host, other));
        var crossed = await host.Http.Ask(Post("/sindri/context/map/import", Body(other, issuedForOther.Hash, (issuedForFile.Challenge, "import map " + issuedForOther.Hash[..6])), Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (replay.Status, replay.Code));
        Assert.Equal(recorded.Value!.Envelope.Payload, afterReplay.Value!.Envelope.Payload);
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (crossed.Status, crossed.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Import_is_protected_a_session_a_human_without_a_terminal_and_a_foreign_origin_are_refused_and_nothing_is_written()
    {
        await using var host = await Approved(sessions: true, TestContext.Current.CancellationToken);
        var file = FixtureMap.WriteTo(host.Home);

        var session = await host.Http.Ask(Post("/sindri/context/map/import", Body(file), (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)));
        var noTerminal = await host.Http.Ask(Post("/sindri/context/map/import", Body(file),
            [.. Human(host.Token.Value).Where(h => h.Name != SindriHeaders.Terminal)]));
        var origin = await host.Http.Ask(Post("/sindri/context/map/import", Body(file), [.. Human(host.Token.Value), ("Origin", "http://evil.example")]));
        var status = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (session.Status, session.Code));
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (noTerminal.Status, noTerminal.Code));
        Assert.Equal(HttpStatusCode.Forbidden, origin.Status);   // the loopback guard refuses a foreign browser Origin before the command chain
        Assert.Equal("SND-CONTEXT-030", status.Code);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Export_lands_in_the_exports_folder_a_session_can_not_export_and_reads_only_its_repos_scope()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: true, ct);
        await Import(host);

        var export = await host.Http.Ask(Get("/sindri/context/map/export", Human(host.Token.Value)));
        var outside = await host.Http.Ask(Get("/sindri/context/map/export?path=%2Ftmp%2Fx.json", Human(host.Token.Value)));
        var scoped = await host.Http.Ask(Get("/sindri/context/map", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)));
        var other = await host.Http.Ask(Get("/sindri/context/map", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "other")));
        var sessionExport = await host.Http.Ask(Get("/sindri/context/map/export", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-CONTEXT-020"), (sessionExport.Status, sessionExport.Code));   // spec §3: an export is a whole-map path
        Assert.Equal(HttpStatusCode.OK, export.Status);
        Assert.Contains(Path.Combine(host.Home, "context", "exports"), JsonDocument.Parse(export.Body).RootElement.GetProperty("path").GetString(), StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-033"), (outside.Status, outside.Code));
        Assert.Contains("\"nodes\":13", scoped.Body, StringComparison.Ordinal);   // its 8 nodes and the 5 surfaces inside its features (part-of descendants)
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (other.Status, other.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Map_status_with_an_anchor_adds_the_lenses_coverage_and_without_one_has_none()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);
        var tabs = Path.Combine(host.Home, "tabs-map.json");
        await File.WriteAllTextAsync(tabs, MapTabsRepo.MapJson, ct);
        await Import(host, tabs);
        var anchor = Uri.EscapeDataString("feature:tabs.tab-manager");

        var plain = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));
        var withAnchor = await host.Http.Ask(Get("/sindri/context/map?anchor=" + anchor, Human(host.Token.Value)));
        var lenses = await host.Http.Ask(Get("/sindri/context/lenses?anchor=" + anchor, Human(host.Token.Value)));
        var unknown = await host.Http.Ask(Get("/sindri/context/map?anchor=nope", Human(host.Token.Value)));

        Assert.False(JsonDocument.Parse(plain.Body).RootElement.TryGetProperty("coverage", out _));
        var coverage = JsonDocument.Parse(withAnchor.Body).RootElement.GetProperty("coverage");
        var lensesCoverage = System.Text.Json.Nodes.JsonNode.Parse(lenses.Body)!["coverage"]!.AsObject();
        Assert.True(lensesCoverage.Remove("map"));   // the repo-file part of the lenses' coverage (C13) is theirs alone: status counts the map's own scope
        Assert.Equal(lensesCoverage.ToJsonString(), coverage.GetRawText());
        Assert.Equal((6, 2, 6, 2), (coverage.GetProperty("scope").GetInt32(), coverage.GetProperty("intent").GetProperty("covered").GetInt32(),
            coverage.GetProperty("product").GetProperty("confirmed").GetInt32(), coverage.GetProperty("code").GetProperty("implemented").GetInt32()));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (unknown.Status, unknown.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_context_purge_service_runs_the_purge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);
        await using var scope = host.App.Services.CreateAsyncScope();

        var purge = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "context-purge");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease

        Assert.True((await purge.Run(new ScheduledRun(grant, DateTimeOffset.UnixEpoch), ct)).IsSuccess);
    }

    internal static async Task Import(GoldenHost host, string? mapFile = null)
    {
        var file = mapFile ?? FixtureMap.WriteTo(host.Home);
        var (hash, challenge, action) = Issued(await PreviewBody(host, file));
        var imported = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, (challenge, action)), Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, imported.Status);
    }

    private static async Task<string> PreviewBody(GoldenHost host, string file) =>
        (await host.Http.Ask(Post("/sindri/context/map/import", Body(file), Human(host.Token.Value)))).Body;

    /// <summary>A host whose approved profile maps the fixture map's repo and 'other'.</summary>
    internal static async Task<GoldenHost> Approved(bool sessions, CancellationToken ct)
    {
        var yaml = $"schemaVersion: 1\nuser: golden\nrepos:\n  - {{ name: {FixtureMap.Repo}, path: /work/{FixtureMap.Repo} }}\n  - {{ name: other, path: /work/other }}\n"
            + $"context:\n  sessions: {{ enabled: {(sessions ? "true" : "false")} }}\n";
        var host = await GoldenHost.Start(temporary: true, ct, yaml);
        var hash = await host.LiveHash(ct);
        var issued = await host.Http.Ask(Post("/sindri/workflow/challenges", $$"""{"action":"approve profile {{hash.Abbreviated}}"}""", Human(host.Token.Value)));
        var challenge = JsonDocument.Parse(issued.Body).RootElement.GetProperty("challenge").GetString();
        var approved = await host.Http.Ask(Post("/sindri/profile/approve",
            $$$"""{"hash":"{{{hash.Value}}}","answer":{"challenge":"{{{challenge}}}","typed":"approve profile {{{hash.Abbreviated}}}"}}""", Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        return host;
    }
}
