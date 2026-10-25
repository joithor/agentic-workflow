using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Features.ContextCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The warm read: one current-map read as a person at the app, so the view a search needs is built before anyone asks.</summary>
public sealed class ContextWarmupTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task With_no_map_yet_the_warm_read_reports_not_warm_and_does_not_throw()
    {
        await using var host = await ContextHosts.Approved(_repo, sessions: false, TestContext.Current.CancellationToken);
        var scope = host.App.Services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var warm = await scope.ServiceProvider.GetRequiredService<ContextWarmup>().Warm(TestContext.Current.CancellationToken);

            Assert.False(warm);   // no map is imported yet: nothing to warm, and no throw
        }
    }
}
