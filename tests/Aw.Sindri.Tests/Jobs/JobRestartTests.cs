using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>A restart mid-job on a real SQLite home (API P6 unit F, D8): the second process on the same home ends the first one's job failed SND-JOB-001, and never re-runs it.</summary>
[Lane(TestLane.Integration)]
public sealed class JobRestartTests
{
    [Fact(Timeout = 60_000)]
    public async Task Restart_mid_job_marks_it_failed_SND_JOB_001()
    {
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempHome();
        Core.Ids.JobId job;
        await using (var first = await JobHost.Start(ct, home.Path))
        {
            (job, _) = await JobHost.StartEcho(first, lines: 100, delayMs: 100);
            await JobHost.ReleaseLease(first, ct);   // the stop released the lease; the job's work dies with the process
        }

        await using var second = await JobHost.Start(ct, home.Path, idScope: 1);
        await ViewScenarios.CatchUp(second, ct);
        var recovered = await second.App.Services.GetRequiredService<JobRecovery>().Run(ct);
        await ViewScenarios.CatchUp(second, ct);
        var view = JsonDocument.Parse((await JobHost.Read(second, $"/sindri/jobs/{job.Value}")).Body).RootElement;
        var ledger = (await second.App.Services.GetRequiredService<ILedgerReader>().ReadAfter(0, 1000, ct)).Value;

        Assert.Equal(1, recovered.Value);
        Assert.Equal(("failed", "SND-JOB-001"), (view.GetProperty("state").GetString(), view.GetProperty("code").GetString()));
        var ends = ledger.Where(static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal)).ToList();
        var end = Assert.Single(ends);
        Assert.Equal("service:jobs", end.Envelope.Actor);
        _ = Assert.Single(ledger, static e => string.Equals(e.Envelope.Type, EventTypes.JobStarted, StringComparison.Ordinal));   // not run again
    }
}
