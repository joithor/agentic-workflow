using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RunService;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.RunService;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>What a start reserved is given back whatever stops it before the work (API P6 review 2, CC-N3): a throw between the admission and the claim, and a work that finds it has no lease or no job.</summary>
[Lane(TestLane.Unit)]
public sealed class ReservationLeakTests
{
    private static readonly ServiceName _tidy = ServiceName.From("tidy");

    [Fact]
    public async Task A_start_that_throws_after_its_admission_rolls_the_admission_back()
    {
        var rollbacks = new CountingAdmission();
        await using var runtime = new RealJobRuntime(scrubber: null, rows: null, configure: (services, harness) =>
            _ = services.AddSingleton(rollbacks).AddScoped<IJobAdmission<ProbeJobRequest>, RollbackCounter>());
        await runtime.Open();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.Start(new ProbeJobRequest("repo-throws"), A.Caller().Human().Build()));

        Expect.Equal(1, rollbacks.Rollbacks);
        Expect.Equal(0, runtime.Get<Features.Jobs.JobRegistry>().Count());
    }

    [Fact]
    public async Task A_work_with_no_job_context_gives_the_reservation_back()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var runtime = await RunServiceRuntime.Open(tidy);
        var guard = runtime.Get<ServiceRunGuard>();
        Assert.True(guard.TryReserveManual(_tidy));
        await using var scope = runtime.Get<IServiceScopeFactory>().CreateAsyncScope();
        var work = new RunServiceService([tidy], guard, new FakeJobContextAccessor(), scope.ServiceProvider.GetRequiredService<IServiceRunRecorder>(), runtime.H.Held, runtime.H.Clock, runtime.Runner);

        var refused = await work.Handle(new RunServiceRequest(_tidy), TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.True(guard.TryClaim(_tidy, out var free));   // the scheduler can run it again
        free.Dispose();
    }

    private sealed class CountingAdmission
    {
        private int _rollbacks;

        public int Rollbacks => Volatile.Read(ref _rollbacks);

        public void Rolled() => _ = Interlocked.Increment(ref _rollbacks);
    }

    private sealed class RollbackCounter(CountingAdmission count) : IJobAdmission<ProbeJobRequest>
    {
        public ValueTask<Kernel.Result<ServiceName?>> Admit(ProbeJobRequest request, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<ServiceName?>>((ServiceName?)null);

        public void Rollback(ProbeJobRequest request) => count.Rolled();
    }
}
