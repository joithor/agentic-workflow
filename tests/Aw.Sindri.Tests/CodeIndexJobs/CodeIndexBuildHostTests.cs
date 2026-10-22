using System.Text.Json;
using Aw.CodeIndex.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.CodeIndexJobs;

/// <summary>The code index on a started host (CI-9): the 'code-index-main-move' service sees a registered repo's trunk move, starts 'index:&lt;repo&gt;' as itself, and the job ends succeeded with that commit.</summary>
public sealed class CodeIndexBuildHostTests : IDisposable
{
    private readonly TempGitRepo _repo = new GitRepoBuilder().WithFile("src/Worker.cs", "namespace Demo; public sealed class Worker { public void Run() => Stop(); public void Stop() { } }\n").Build();
    private readonly DirectoryInfo _locks = Directory.CreateTempSubdirectory("aw-index-locks-");

    public void Dispose()
    {
        _repo.Dispose();
        _locks.Delete(recursive: true);
    }

    [Fact(Timeout = 180_000), Trait("Lane", "integration")]
    public async Task A_moved_head_starts_an_index_job_that_ends_succeeded_with_that_commit_and_the_next_commit_builds_again()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ContextHosts.Approved(_repo.Path, sessions: false, ct, name: "demo", extra: services =>
        {
            // why: the build would otherwise take a slot of the box's real heavy-job lock
            _ = services.RemoveAll<HeavyLockSettings>();
            _ = services.AddSingleton(new HeavyLockSettings(Path.Combine(_locks.FullName, "heavy-job.lock"), 2, Held: false));
        });

        var first = _repo.Head;
        var started = await Tick(host, ct);
        var firstJob = await Ended(host, expectedJobs: 1, ct);
        _repo.WriteFile("src/Worker.cs", "namespace Demo; public sealed class Worker { public void Run() => Stop(); public void Stop() => Go(); void Go() { } }\n");
        var second = _repo.Commit("second");
        _ = await Tick(host, ct);
        var secondJob = await Ended(host, expectedJobs: 2, ct);
        var quiet = await Tick(host, ct);

        Assert.False(started.Value.Idle);
        Assert.Equal((JobState.Succeeded, "published", first.Value), (firstJob.State, Field(firstJob, "outcome"), Field(firstJob, "commit")));
        Assert.Equal((JobState.Succeeded, "published", second.Value), (secondJob.State, Field(secondJob, "outcome"), Field(secondJob, "commit")));
        Assert.True(quiet.Value.Idle);
        Assert.Equal(2, await Built(host, ct));
    }

    // One scheduler tick of the service, as the lease holder: the host runs no scheduler of its own in a test.
    private static async Task<Aw.Kernel.Result<ScheduledResult>> Tick(GoldenHost host, CancellationToken ct)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "code-index-main-move");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        using var caller = host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("code-index-main-move");
        return await service.Run(new ScheduledRun(grant, DateTimeOffset.UtcNow), ct);
    }

    private static string? Field(JobRow job, string name) => job.Result?.TryGetProperty(name, out var value) == true ? value.GetString() : null;

    private static async Task<JobRow> Ended(GoldenHost host, int expectedJobs, CancellationToken ct)
    {
        var views = host.App.Services.GetRequiredService<IJobViews>();
        for (var i = 0; i < 400; i++)
        {
            var jobs = (await views.List(state: null, JobKind.From("BuildCodeIndex"), ct)).Value;
            if (jobs.Count == expectedJobs && jobs.All(static j => j.State != JobState.Running))
            {
                return jobs[0];
            }

            await Task.Delay(250, ct);
        }

        throw new TimeoutException("the index job didn't end");
    }

    private static async Task<int> Built(GoldenHost host, CancellationToken ct)
    {
        var ledger = host.App.Services.GetRequiredService<ILedgerStore>();
        return (await ledger.ReadAfter(0, 1000, ct)).Value!.Count(static e => e.Envelope.Type == EventTypes.CodeIndexBuilt);
    }
}
