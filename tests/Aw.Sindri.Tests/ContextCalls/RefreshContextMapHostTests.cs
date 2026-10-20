using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.ContextCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The refresh job on a started host: the 'context-map-refresh' service starts it through the command chain as itself, with no challenge or lease prompt (sindri-api's request, owner decision 2026-10-19).</summary>
public sealed class RefreshContextMapHostTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_service_caller_starts_RefreshContextMap_with_no_challenge_and_the_job_is_recorded_as_that_service()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ContextHosts.Approved(_repo, sessions: false, ct);

        Kernel.Result<JobStartedResponse> started;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            using var caller = host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("context-map-refresh");
            started = await scope.ServiceProvider.GetRequiredService<Kernel.Services.IService<RefreshContextMapRequest, JobStartedResponse>>().Handle(new RefreshContextMapRequest(), ct);
        }

        var ledger = host.App.Services.GetRequiredService<ILedgerStore>();
        var begun = await ledger.Latest(EventTypes.JobStarted, ct);
        Assert.True(started.IsSuccess, started.IsError ? $"{started.Error.Code}: {started.Error.Message}" : null);
        Assert.Equal("service:context-map-refresh", begun.Value!.Envelope.Actor);
        await WaitForJobToEnd(ledger, ct);
    }

    // The job ends on its own (a failure is fine here: the start is what this test pins); wait so the host isn't disposed under it.
    private static async Task WaitForJobToEnd(ILedgerStore ledger, CancellationToken ct)
    {
        for (var i = 0; i < 160; i++)
        {
            if ((await ledger.Latest(EventTypes.JobFinished, ct)).Value is not null)
            {
                return;
            }

            await Task.Delay(250, ct);
        }

        Assert.Fail("the refresh job didn't end");
    }
}
