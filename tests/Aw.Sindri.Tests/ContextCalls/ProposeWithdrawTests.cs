using System.Net;
using System.Reflection;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>A scoping run with no surfaces takes its undecided proposals back and Sindri records it once (scoping spec C9).</summary>
public sealed class ProposeWithdrawTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    [Fact, Trait("Lane", "unit")]
    public void ProposeContextMapService_depends_on_IScopeProposals_not_IContextEngine()
    {
        var parameters = typeof(ProposeContextMapRequest).Assembly.GetType("Aw.Sindri.Features.ContextCalls.ProposeContextMapService")!
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Contains(typeof(IScopeProposals), parameters);
        Assert.DoesNotContain(typeof(IContextEngine), parameters);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Propose_empty_records_context_map_proposed_with_withdrawn_once()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ContextHosts.Approved(_repo, sessions: true, ct,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        (string Name, string Value)[] app = [.. Human(host.Token.Value, client: "ui").Where(h => h.Name != SindriHeaders.Terminal)];
        var proposals = $$"""{"run":"scope-1","repo":"{{FixtureMap.Repo}}","surfaces":[{"name":"Zoom","kind":"feature","evidence":[]}]}""";
        var empty = $$"""{"run":"scope-1","repo":"{{FixtureMap.Repo}}","surfaces":[]}""";
        var ledger = host.App.Services.GetRequiredService<ILedgerStore>();

        var proposed = await host.Http.Ask(Post("/sindri/context/map/proposals", proposals, app));
        var first = await host.Http.Ask(Post("/sindri/context/map/proposals", empty, app));
        var recorded = await ledger.Latest(EventTypes.ContextMapProposed, ct);
        var second = await host.Http.Ask(Post("/sindri/context/map/proposals", empty, app));
        var after = await ledger.Latest(EventTypes.ContextMapProposed, ct);

        Assert.Equal(HttpStatusCode.OK, proposed.Status);
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(1, JsonDocument.Parse(first.Body).RootElement.GetProperty("result").GetProperty("withdrawn").GetInt32());
        Assert.Contains("\"withdrawn\":1", recorded.Value!.Envelope.Payload, StringComparison.Ordinal);
        Assert.Equal(0, JsonDocument.Parse(second.Body).RootElement.GetProperty("result").GetProperty("withdrawn").GetInt32());
        Assert.Equal(recorded.Value.Sequence, after.Value!.Sequence);   // the second withdrawal found nothing to take back: no new event
    }
}
