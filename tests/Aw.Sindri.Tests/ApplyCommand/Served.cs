using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Client;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// A Sindri home served the way 'sindri serve' serves it (engine P4): the HTTP host over the home (with an optional
/// override of its registrations) listening on a free loopback port, the lease and this start's human token held,
/// control.json naming the API and the dev server, and the worker running. The 'workflow' verbs run in process as the
/// CLI runs them: the writes are clients of the API, as a human at a terminal unless a test says otherwise.
/// </summary>
internal sealed class Served : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop;
    private readonly TemporalEndpoint _temporal;
    private CancellationTokenSource _workerStop;
    private Task _worker;

    private Served(WebApplication app, WorkflowEnvironment environment, TemporalEndpoint temporal, CancellationTokenSource stop)
    {
        App = app;
        Environment = environment;
        _stop = stop;
        _temporal = temporal;
        _workerStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        _worker = app.Services.GetRequiredService<ITemporalWorker>().Run(temporal, _workerStop.Token);
        Http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
    }

    public IServiceProvider Sindri => App.Services;

    public WebApplication App { get; }

    public WorkflowEnvironment Environment { get; }

    /// <summary>A client of the API, without caller headers (a test adds them per request).</summary>
    public HttpClient Http { get; }

    /// <summary>Serves <paramref name="home"/> on <paramref name="environment"/>.</summary>
    public static async Task<Served> Start(string home, WorkflowEnvironment environment, Action<IServiceCollection>? replace = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var app = LoopbackBinding.Build([], name => name == "AW_SINDRI_HOME" ? home : null, new FakeTimeProvider(), replace);
        _ = Pipeline.Configure(app);
        await WorkflowsHarness.HoldLease(app.Services, ct);
        app.Services.GetRequiredService<HeldHumanToken>().Hold((await app.Services.GetRequiredService<IHumanTokenStore>().Mint(ct)).Value);
        await app.StartAsync(ct);
        var epoch = app.Services.GetRequiredService<HeldLease>().Current!.Epoch;   // why: HoldLease just held it
        var temporal = await WorkflowsHarness.AsServe(app.Services, environment, new Uri(app.Urls.First()), epoch, ct);
        return new Served(app, environment, temporal, CancellationTokenSource.CreateLinkedTokenSource(ct));
    }

    /// <summary>Runs 'sindri workflow &lt;args&gt;' as a human at a terminal.</summary>
    public Task<CliRun> Cli(params string[] args) => As(session: null, terminal: true, typed: null, args);

    /// <summary>Runs 'sindri workflow &lt;args&gt;' as a human at a terminal who types <paramref name="typed"/> when a challenge asks.</summary>
    public Task<CliRun> Typing(string typed, params string[] args) => As(session: null, terminal: true, typed, args);

    /// <summary>Runs 'sindri workflow &lt;args&gt;' inside an agent session (the CLI classes itself a session for <paramref name="reason"/>).</summary>
    public Task<CliRun> AsSession(string reason, params string[] args) => As(reason, terminal: true, typed: null, args);

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

    /// <summary>Stops the worker and starts a new one, as a serve restart's worker would.</summary>
    public async Task RestartWorker()
    {
        await _workerStop.CancelAsync();
        await _worker;
        _workerStop.Dispose();
        _workerStop = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        _worker = Sindri.GetRequiredService<ITemporalWorker>().Run(_temporal, _workerStop.Token);
    }

    /// <summary>Enters a human at a terminal as the caller, for a test that calls a slice in process through its chain.</summary>
    public IDisposable AsHumanAtTerminal() =>
        Sindri.GetRequiredService<ICallerContextAccessor>().Enter(A.Caller().AtTerminal().Client(ClientLabel.Cli).Build());

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _worker;
        _workerStop.Dispose();
        _stop.Dispose();
        Http.Dispose();
        await App.StopAsync(TestContext.Current.CancellationToken);
        await App.DisposeAsync();
    }

    private async Task<CliRun> As(string? session, bool terminal, string? typed, string[] args)
    {
        var (exit, stdout, stderr) = await WorkflowsHarness.Cli(Sindri, new AgentSession(session, terminal, Prover: false), typed, args);
        return new CliRun(exit, stdout, stderr);
    }
}
