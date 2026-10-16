using System.Globalization;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The <see cref="InlineJobRunner"/> as a runner subject (API P6 review T8).</summary>
internal sealed class InlineJobRunnerSubject : IJobRunnerSubject
{
    private readonly SliceHarness _h = new();
    private readonly ServiceProvider _provider;
    private readonly InlineJobRunner _runner;
    private readonly JobDefinition _definition;

    private InlineJobRunnerSubject()
    {
        _provider = new ServiceCollection()
            .AddSingleton(AwJson.CreateOptions(EchoJsonContext.Default))
            .AddSingleton(_h.Callers)
            .AddSingleton<TimeProvider>(_h.Clock)
            .AddSingleton<IJobContextAccessor>(new FakeJobContextAccessor())
            .AddSingleton<EchoJobProbe>()
            .AddJob<EchoJobRequest, EchoReport, EchoJobService>(static _ => JobKey.From("echo"), static r => string.Create(CultureInfo.InvariantCulture, $"echoed {r.Lines}"))
            .BuildServiceProvider();
        _runner = new InlineJobRunner(_provider.GetRequiredService<IServiceScopeFactory>(), _provider.GetRequiredService<IJobContextAccessor>(), _h.Callers);
        _definition = _provider.GetServices<JobDefinition>().Single();
    }

    public IJobRunner Runner => _runner;

    public EchoJobProbe Probe => _provider.GetRequiredService<EchoJobProbe>();

    public static async ValueTask<InlineJobRunnerSubject> Open()
    {
        var subject = new InlineJobRunnerSubject();
        _ = await subject._h.HoldLease();
        return subject;
    }

    public async Task Run(JobId job, EchoJobRequest request, CallerContext caller, ServiceName? service) =>
        await _runner.Start(new JobStart(job, JobKind.From("EchoJob"), _definition.KeyOf(request), _definition, request, caller, _h.Held.Current!, service), TestContext.Current.CancellationToken);

    public Task Settle() => Task.CompletedTask;

    public IReadOnlyList<string> Lines(JobId job) => [.. _runner.Lines.Where(l => l.Job == job).Select(static l => l.Line)];

    public JobEnd? End(JobId job) =>
        _runner.Finished.Where(f => f.Job == job).Select(static f => new JobEnd(f.Result.Error is null, f.Result.Error?.Code)).FirstOrDefault();

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
