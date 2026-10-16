using System.Globalization;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>AddJob's registration and the inline runner (API P6 contract): a job registered through AddJob runs its raw work, with the starting caller and a job context, and its result is kept as JSON.</summary>
[Lane(TestLane.Unit)]
public sealed class InlineJobRunnerTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider Provider() => new ServiceCollection()
        .AddSingleton(AwJson.CreateOptions(EchoJsonContext.Default))
        .AddSingleton(_h.Callers)
        .AddSingleton<TimeProvider>(_h.Clock)
        .AddSingleton<IJobContextAccessor>(new FakeJobContextAccessor())
        .AddSingleton<EchoJobProbe>()
        .AddJob<EchoJobRequest, EchoReport, EchoJobService>(static _ => JobKey.From("echo"), static r => string.Create(CultureInfo.InvariantCulture, $"echoed {r.Lines}"))
        .BuildServiceProvider();

    [Fact]
    public async Task A_registered_job_runs_inline_under_the_starting_caller_with_its_progress_and_result()
    {
        await using var provider = Provider();
        var grant = await _h.HoldLease();
        var caller = A.Caller().Human().Build();
        var runner = new InlineJobRunner(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<IJobContextAccessor>(), _h.Callers);
        var definition = provider.GetServices<JobDefinition>().Single();
        var request = new EchoJobRequest(Lines: 2, DelayMs: 0);

        await runner.Start(new JobStart(A.FirstJobId, JobKind.From("EchoJob"), definition.KeyOf(request), definition, request, caller, grant, Service: null), Token);

        Expect.Sequence(["echo 1", "echo 2"], runner.Lines.Select(l => l.Line));
        var (_, result) = Expect.Only(runner.Finished);
        Assert.Null(result.Error);
        Expect.Equal("echoed 2", result.Summary);
        Expect.Equal(2, result.Result!.Value.GetProperty("lines").GetInt32());
        Expect.Sequence([caller.Actor], provider.GetRequiredService<EchoJobProbe>().Actors);
        Expect.Equal("echo", definition.KeyOf(request).Value);
    }
}
