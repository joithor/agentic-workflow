using System.Net;
using System.Text.Json;
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
    private static string Body(string path, string? hash = null, bool confirmed = false) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = path, ["hash"] = hash, ["confirmed"] = confirmed });

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Without_a_map_status_is_SND_CONTEXT_030()
    {
        await using var host = await Approved(sessions: false, TestContext.Current.CancellationToken);

        var status = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-030"), (status.Status, status.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_human_at_a_terminal_previews_then_imports_with_the_confirmed_hash_and_the_import_is_recorded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);
        var file = FixtureMap.WriteTo(host.Home);

        var preview = await host.Http.Ask(Post("/sindri/context/map/import", Body(file), Human(host.Token.Value)));
        var hash = JsonDocument.Parse(preview.Body).RootElement.GetProperty("preview").GetProperty("hash").GetString()!;   // why: a preview always carries the hash
        var unconfirmed = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash), Human(host.Token.Value)));
        var imported = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, confirmed: true), Human(host.Token.Value)));
        var status = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapImported, ct);

        Assert.Equal(HttpStatusCode.OK, preview.Status);
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (unconfirmed.Status, unconfirmed.Code));
        Assert.Equal(HttpStatusCode.OK, imported.Status);
        Assert.Contains("\"status\":\"current\"", imported.Body, StringComparison.Ordinal);
        Assert.Contains("\"nodes\":19", status.Body, StringComparison.Ordinal);
        Assert.Contains(hash, recorded.Value!.Envelope.Payload, StringComparison.Ordinal);
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
    public async Task Export_lands_in_the_exports_folder_and_a_session_reads_only_its_repos_scope()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: true, ct);
        await Import(host);

        var export = await host.Http.Ask(Get("/sindri/context/map/export", Human(host.Token.Value)));
        var outside = await host.Http.Ask(Get("/sindri/context/map/export?path=%2Ftmp%2Fx.json", Human(host.Token.Value)));
        var scoped = await host.Http.Ask(Get("/sindri/context/map", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)));
        var other = await host.Http.Ask(Get("/sindri/context/map", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "other")));

        Assert.Equal(HttpStatusCode.OK, export.Status);
        Assert.Contains(Path.Combine(host.Home, "context", "exports"), JsonDocument.Parse(export.Body).RootElement.GetProperty("path").GetString(), StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-033"), (outside.Status, outside.Code));
        Assert.Contains("\"nodes\":5", scoped.Body, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (other.Status, other.Code));
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

    private static async Task Import(GoldenHost host)
    {
        var file = FixtureMap.WriteTo(host.Home);
        var preview = await host.Http.Ask(Post("/sindri/context/map/import", Body(file), Human(host.Token.Value)));
        var hash = JsonDocument.Parse(preview.Body).RootElement.GetProperty("preview").GetProperty("hash").GetString();
        var imported = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash, confirmed: true), Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, imported.Status);
    }

    /// <summary>A host whose approved profile maps the fixture map's repo and 'other'.</summary>
    private static async Task<GoldenHost> Approved(bool sessions, CancellationToken ct)
    {
        var yaml = $"schemaVersion: 1\nuser: golden\nrepos:\n  - {{ name: {FixtureMap.Repo}, path: /work/{FixtureMap.Repo} }}\n  - {{ name: other, path: /work/other }}\n"
            + $"context:\n  sessions: {{ enabled: {(sessions ? "true" : "false")} }}\n";
        var host = await GoldenHost.Start(temporary: true, ct, yaml);
        var hash = (await host.LiveHash(ct)).Value;
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", $"{{\"hash\":\"{hash}\",\"confirmed\":true}}", Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        return host;
    }
}
