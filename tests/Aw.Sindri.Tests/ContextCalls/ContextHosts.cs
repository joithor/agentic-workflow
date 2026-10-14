using System.Net;
using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Started hosts whose approved profile maps the fixture repo, for the context surfaces' tests (HTTP and MCP).</summary>
internal static class ContextHosts
{
    /// <summary>A host whose profile maps <paramref name="repo"/> as split-panes, approved by the human, with one context-docs-refresh run done.</summary>
    /// <param name="repo">The fixture repo.</param>
    /// <param name="sessions">'context.sessions.enabled'.</param>
    /// <param name="ct">Cancels the start.</param>
    /// <param name="extra">More service registrations, or null.</param>
    /// <returns>The started host.</returns>
    internal static async Task<GoldenHost> Approved(FixtureRepo repo, bool sessions, CancellationToken ct, Action<IServiceCollection>? extra = null)
    {
        var yaml = $"schemaVersion: 1\nuser: golden\nrepos:\n  - {{ name: split-panes, path: \"{repo.Path}\" }}\n"
            + $"context:\n  docs: {{ globs: [\"docs/**/*.md\"] }}\n  sessions: {{ enabled: {(sessions ? "true" : "false")} }}\n";
        var host = await GoldenHost.Start(temporary: true, ct, yaml, extra);
        var hash = (await host.LiveHash(ct)).Value;
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        await using var scope = host.App.Services.CreateAsyncScope();
        var refresh = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "context-docs-refresh");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        Assert.True((await refresh.Run(new ScheduledRun(grant, DateTimeOffset.UnixEpoch), ct)).IsSuccess);
        return host;
    }
}
