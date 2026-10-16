using System.Globalization;
using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>What the job and service verb tests share: the verbs over a <see cref="JobApi"/>, their output, the two Ctrl-C tokens and a counting fake clock.</summary>
internal sealed class JobRig : IDisposable
{
    internal const string Id = "JOB-0123456789abcdef0123456789abcdef";

    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly CancellationTokenSource _interrupt = new();
    private readonly CancellationTokenSource _again = new();

    public StringWriter Out { get; } = new();

    public StringWriter Error { get; } = new();

    public FollowRig.CountingClock Clock { get; } = new();

    public CancellationToken Interrupted => _interrupt.Token;

    public CancellationToken Again => _again.Token;

    public static JobStartedResponse Started { get; } = new(new CommandAccepted(CommandId.From(new Guid(7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), 10), JobId.From(Id),
        new JobLinks("/sindri/jobs/" + Id, "/sindri/jobs/" + Id + "/cancel", "/sindri/events?after=10&types=job.*"));

    public void Dispose()
    {
        _interrupt.Dispose();
        _again.Dispose();
        Out.Dispose();
        Error.Dispose();
    }

    public static string Body<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));

    public static string View(JobState state, int rounds, string? summary = null, string? code = null) => Body(new JobView(JobId.From(Id), JobKind.From("RunService"), JobKey.From("heartbeat"),
        Item: null, Repo: null, ServiceName.From("heartbeat"), state, "human:local", KnownInstants.Epoch, state == JobState.Running ? null : KnownInstants.Epoch,
        [.. Enumerable.Range(1, rounds).Select(static n => new JobLine(n, string.Create(CultureInfo.InvariantCulture, $"line {n}"), KnownInstants.Epoch))], rounds, summary, code, Result: null, CancelledBy: null,
        state == JobState.Running ? [ActionId.From("CancelJob")] : [], state == JobState.Running ? "sindri jobs cancel " + Id : null, new AsOf(10, 10, KnownInstants.Epoch, Rebuilding: false, Stalled: false)));

    public static string Progress(int round, string line) => string.Create(CultureInfo.InvariantCulture, $$"""{"commandId":"00000000-0000-0000-0000-000000000007","job":"{{Id}}","round":{{round}},"line":"{{line}}"}""");

    public static string Finished(string outcome, string summary, string? code = null) =>
        $$"""{"commandId":"00000000-0000-0000-0000-000000000007","job":"{{Id}}","outcome":"{{outcome}}","summary":"{{summary}}","code":{{(code is null ? "null" : "\"" + code + "\"")}}}""";

    public static string Cancelled() => $$"""{"commandId":"00000000-0000-0000-0000-000000000008","job":"{{Id}}","by":"human:local"}""";

    public static ControlPlaneClient Client(JobApi api) => new(_probe, SliceHarness.Json,
        CallerHeaders.For(new AgentSession(Signal: null, Terminal: true, Prover: false), new InMemoryHumanTokenStore(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);

    public Output Output() => new(Out, Error, SliceHarness.Json);

    public JobFollower Follower(JobApi api) => new(Client(api), Output(), Clock);

    public ServiceVerbs Services(JobApi api) => new(Client(api), Output(), Follower(api), (_, _) => Task.FromResult<Kernel.Error?>(null));

    public JobVerbs Jobs(JobApi api) => new(Client(api), Output(), (_, _) => Task.FromResult<Kernel.Error?>(null));

    public Task Interrupt() => _interrupt.CancelAsync();

    public Task SecondInterrupt() => _again.CancelAsync();

    public static Task<int> Finished(Task<int> run) => run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

    public async Task Tick(TimeSpan step, Func<bool> done)
    {
        for (var i = 0; i < 1_000 && !done(); i++)
        {
            Clock.Advance(step);
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(done(), "the condition never held while the clock moved");
    }

    public Task<int> RunServices(JobApi api, params string[] args) => Task.Run(() => Services(api).Run(args, Interrupted, Again));

    public Task<int> RunJobs(JobApi api, params string[] args) => Task.Run(() => Jobs(api).Run(args, Interrupted));

    public static Reply Refused(HttpStatusCode status, string code, string detail) => new(status, string.Create(CultureInfo.InvariantCulture, $$"""{"title":"{{code}}","status":{{(int)status}},"detail":"{{detail}}"}"""));
}
