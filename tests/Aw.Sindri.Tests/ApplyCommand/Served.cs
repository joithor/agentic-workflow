using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Client;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// A Sindri home served the way 'sindri serve' serves it (engine P4 unit (b)): the composed host over the home (with an
/// optional override of its registrations), the lease, control.json naming the dev server, and the worker running.
/// The 'workflow' verbs run in process against it.
/// </summary>
internal sealed class Served : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop;
    private readonly Task _worker;

    private Served(ServiceProvider sindri, WorkflowEnvironment environment, CancellationTokenSource stop, Task worker)
    {
        Sindri = sindri;
        Environment = environment;
        _stop = stop;
        _worker = worker;
    }

    public ServiceProvider Sindri { get; }

    public WorkflowEnvironment Environment { get; }

    /// <summary>Serves <paramref name="home"/> on <paramref name="environment"/>.</summary>
    public static async Task<Served> Start(string home, WorkflowEnvironment environment, Action<IServiceCollection>? replace = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var services = Composition.Compose(new ServiceCollection(), name => name == "AW_SINDRI_HOME" ? home : null, new FakeTimeProvider());
        replace?.Invoke(services);
        var sindri = services.BuildServiceProvider();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var temporal = await WorkflowsHarness.AsServe(sindri, environment, ct);
        var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var worker = sindri.GetRequiredService<ITemporalWorker>().Run(temporal, stop.Token);
        return new Served(sindri, environment, stop, worker);
    }

    /// <summary>Runs 'sindri workflow &lt;args&gt;'.</summary>
    public async Task<CliRun> Cli(params string[] args)
    {
        var (exit, stdout, stderr) = await WorkflowsHarness.Cli(Sindri, args);
        return new CliRun(exit, stdout, stderr);
    }

    /// <summary>The instance at <paramref name="address"/>.</summary>
    public WorkflowHandle<RecipeWorkflow> Handle(string address) => Environment.Client.GetWorkflowHandle<RecipeWorkflow>(address);

    /// <summary>The instance as 'GetInstance' shows it once <paramref name="holds"/>.</summary>
    public Task<InstanceView> Until(string address, Func<InstanceView, bool> holds) => WorkflowsHarness.Until(Handle(address), holds);

    /// <summary>The 'workflow.*' ledger events of <paramref name="address"/>, in order.</summary>
    public async Task<IReadOnlyList<string>> Events(string address)
    {
        var read = await Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, TestContext.Current.CancellationToken);
        Assert.False(read.IsError, read.IsError ? read.Error.Message : null);
        return [.. read.Value
            .Where(e => e.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal) && e.Envelope.Payload.Contains($"\"{address}\"", StringComparison.Ordinal))
            .Select(static e => e.Envelope.Type)];
    }

    /// <summary>Reports <paramref name="outcome"/> on the open desk run, with <paramref name="output"/> as its one output when given.</summary>
    public async Task<CliRun> Report(string address, string outcome, (string Fact, string File)? output = null)
    {
        var open = await Until(address, static v => v.Task is not null);
        string[] args = ["complete", address, "--run", open.Run!.Value.Value, "--outcome", outcome];
        var run = await Cli(output is { } o ? [.. args, "--output", $"{o.Fact}={o.File}"] : args);
        Assert.True(run.Exit == 0, run.Stderr);
        return run;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _worker;
        _stop.Dispose();
        await Sindri.DisposeAsync();
    }
}
