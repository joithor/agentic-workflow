using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The context engine's run proposals honour the run-proposals contract, over a started host whose profile maps the fixture repo and whose map is imported.</summary>
[Lane(TestLane.Integration)]
public sealed class ScopeRunProposalsContractTests : ScopeRunProposalsContract, IAsyncLifetime
{
    private readonly FixtureRepo _fixture = FixtureRepo.Build();
    private GoldenHost? _host;
    private AsyncServiceScope _scope;

    protected override RepoName Repo => RepoName.From(Context.Testing.Map.FixtureMap.Repo);

    public async ValueTask InitializeAsync()
    {
        _host = await ContextHosts.Approved(_fixture, sessions: true, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: Context.Testing.Map.FixtureMap.Repo);
        await ContextMapApiTests.Import(_host);
        _scope = _host.App.Services.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        _fixture.Dispose();
    }

    protected override IScopeRunProposals Create() => _scope.ServiceProvider.GetRequiredService<IScopeRunProposals>();
}
