using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The agent runtime in the interpreter (engine P8 U3; spec §5.3, §6.5): a Step whose descriptor says 'agent' is launched once, watched, and
/// closed by its session's own 'complete' report, or ended as 'exit' or 'lost'. FakeLauncher, FakeAgentSessions and FakeRunDiff stand in for the
/// real adapters; the polls are 100 ms.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class AgentRuntimeInterpreterTests(SindriTimeSkippingServer skipping) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private const string RecipeName = "workflow.dev-agent";
    private const string Repo = "/work/app";
    private static readonly ItemInfo _item = new("T-1", "ITM-00000000000000000000000000000007", "app");
    private readonly WorkflowsHarness _harness = new();
    private readonly FakeLauncher _launcher = new();
    private readonly FakeAgentSessions _sessions = new();
    private readonly FakeRunDiff _diff = new();
    private string _root = RecipeName;   // the recipe a rig starts; a test that wants a parent over a child sets it and _also before Rig
    private string[] _also = [];
    private ILauncher? _launcherSeen;   // what the host sees as the launcher when a test wraps the fake
    private IRunDiff? _diffSeen;

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task An_agent_step_launches_once_and_its_report_moves_the_instance()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            var result = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(view.Run!.Value)), new WorkflowUpdateOptions(new string('a', 32)));
            await WaitFor(() => _launcher.Stops.Count == 1, ct);

            var request = Assert.Single(_launcher.Requests);
            Assert.Equal(("done", false, LaunchMode.Open, rig.Worktree), (result.Instance.State, request.Attended, request.Mode, request.Worktree));
            Assert.Equal(rig.Launch, request.Launch);
            Assert.Equal(rig.Launch, Assert.Single(_launcher.Stops));   // the session closes with its run
            Assert.Equal(0, _launcher.SessionCount);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_session_that_ends_without_a_report_is_step_failed_exit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _sessions.End(rig.Launch, SessionEndReason.Exit, exitCode: 1);

            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal("exit", await rig.FailureReason(ct));
            Assert.Equal(ParkReason.NeedsAnswer, parked.Question?.Reason);
            await WaitFor(() => _launcher.Stops.Contains(rig.Launch), ct);   // the run closed, so its session is stopped
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_lost_session_is_step_failed_lost()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _sessions.End(rig.Launch, SessionEndReason.Lost);

            _ = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal("lost", await rig.FailureReason(ct));
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_worker_restart_reattaches_and_starts_no_second_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        var handle = await rig.Start();
        using (var first = rig.Worker())
        {
            await first.ExecuteAsync(async () =>
            {
                _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
                await Launched(ct);
            }, ct);   // the worker stops here, with the session live
        }

        using var second = rig.Worker();
        await second.ExecuteAsync(async () =>
        {
            _sessions.End(rig.Launch, SessionEndReason.Exit, exitCode: 0);
            _ = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal("exit", await rig.FailureReason(ct));
            _ = Assert.Single(_launcher.Requests);   // the replay launched nothing
            await AgentRig.Replayed(handle);
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_relaunch_of_the_live_session_with_no_chosen_id_keeps_the_recorded_session_and_does_not_park()
    {
        var ct = TestContext.Current.CancellationToken;
        var chosen = SessionId.From("sess-i28");
        _launcher.PreChosenSession = chosen;
        _launcherSeen = new SkippedOnRelaunch(_launcher);   // #295 I28: a launcher that did not start the session answers it with a Skipped viewer and no chosen id
        _diffSeen = new FailsOnce(_diff);   // the first launch attempt dies after the outcome is recorded, so the activity retries
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            await Launched(ct);
            var parked = (await handle.QueryAsync(static wf => wf.GetInstance())).Question;

            Assert.Null(parked);
            Assert.Equal([chosen.Value], await rig.StartedSessions(ct));   // one outcome, and it keeps the id of the first
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(view.Run!.Value)), new WorkflowUpdateOptions(new string('b', 32)));
            await WaitFor(() => _launcher.Stops.Count == 1, ct);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Takeover_keeps_the_session_and_handback_resumes_the_watch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await WaitFor(() => _launcher.Requests.Count > 0, ct);   // the takeover may land in the launch phase or in the watch: the invariant below holds for both
            var takeover = new TakeoverRequested("human:cli", new CommandStamp(CommandNonce.From(new string('1', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(takeover), new WorkflowUpdateOptions(new string('1', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "taken-over");

            var handback = new HandbackRequested("human:cli", new CommandStamp(CommandNonce.From(new string('2', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.HandbackAsync(handback), new WorkflowUpdateOptions(new string('2', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "write" && v.Run is not null);
            await WaitFor(() => _launcher.SessionCount == 1, ct);
            Assert.Equal(1, _launcher.SessionCount);   // one live session, however often the launch was requested
            _sessions.End(rig.Launch, SessionEndReason.Exit, exitCode: 0);   // the resumed watch sees it

            _ = await WorkflowsHarness.Until(handle, static v => v.Question is not null);
            string? reason = null;
            var deadline = RealTime.NowDateTime.AddSeconds(30);
            while (reason is null && RealTime.NowDateTime < deadline)   // the mirror row follows the park
            {
                reason = await rig.FailureReason(ct);
                await Task.Delay(50, ct);
            }

            Assert.Equal("exit", reason);
            _ = Assert.Single(_launcher.Requests.Select(static r => r.Launch).Distinct());   // a relaunch after the handback is the same launch id, which the launcher keeps idempotent
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task The_state_timeout_parks_and_the_session_stays()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct, skipping.Environment);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);

            await WorkflowsHarness.Skip(skipping.Environment, TimeSpan.FromMinutes(2), ct);
            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal(ParkReason.Timeout, parked.Question?.Reason);
            Assert.Equal((0, 1), (_launcher.Stops.Count, _launcher.SessionCount));   // a park keeps the session
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Cancel_stops_the_launch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            var stamp = new CommandStamp(CommandNonce.From(new string('c', 32)), new string('1', 64));

            var cancelled = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(new CancelRequested("human:cli", CancelOrigin.Command, stamp, Sequence: null, Parent: null)), new WorkflowUpdateOptions(new string('c', 32)));

            Assert.Equal("cancelled", cancelled.Instance.State);
            await WaitFor(() => _launcher.Stops.Contains(rig.Launch), ct);
            Assert.Equal(0, _launcher.SessionCount);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Usage_over_budget_parks_with_budget()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _sessions.Spend(rig.Launch, sample: 1, tokens: 5_000);   // the state's budget is 1000

            var result = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(view.Run!.Value)), new WorkflowUpdateOptions(new string('b', 32)));

            Assert.Equal(("parked", ParkReason.Budget), (result.Instance.State, result.Instance.Question?.Reason));
        });
    }

    /// <summary>A history recorded after the block runtime and before this one has no 'p8-agent-runtime' marker: it replays on the desk path, as recorded.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_history_before_P8_replays_on_the_desk()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("histories/dev-timeout-before-p8.json")!;
        using var reader = new StreamReader(stream);
        var history = Temporalio.Common.WorkflowHistory.FromJson("T-1/workflow.dev-timeout/1", await reader.ReadToEndAsync(ct));

        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(history, throwOnReplayFailure: false, ct);

        Assert.Null(replayed.ReplayFailure);
    }

    private static StepCompleted Report(RunToken run)
    {
        var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
        return new StepCompleted("agent:session", run, OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:resolution")] = output }, Tokens: 0, StepRuntime.Agent, new string('f', 64));
    }

    // The launcher has been asked and the launch activity has had time to finish; a command that lands before it does is applied in the launch phase, which the tests that need the watch avoid.
    [Fact(Timeout = 120_000)]
    public async Task An_agent_report_that_enters_a_child_wait_starts_the_child()
    {
        var ct = TestContext.Current.CancellationToken;
        _root = "workflow.dev-agent-parent";
        _also = [ChildRecipes.Child(), _parentRecipe];
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(view.Run!.Value)), new WorkflowUpdateOptions(new string('a', 32)));

            var parent = await WorkflowsHarness.Until(handle, static v => v.State == "run" && v.Children.Count == 1);

            Assert.Equal("workflow.t-child", Assert.Single(parent.Children).Address.Value.Split('/')[^1].Split('.')[0]);
        });
    }

    private async Task Launched(CancellationToken ct)
    {
        await WaitFor(() => _launcher.Requests.Count > 0, ct);
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
    }

    private static async Task WaitFor(Func<bool> condition, CancellationToken ct, int seconds = 30)
    {
        var deadline = RealTime.NowDateTime.AddSeconds(seconds);
        while (!condition() && RealTime.NowDateTime < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.True(condition());
    }

    private Task<AgentRig> Rig(CancellationToken ct, WorkflowEnvironment? environment = null) => AgentRig.Start(this, environment, ct);

    /// <summary>The real launcher's answer to a launch id whose session is already live and was not started by this process: that session, a Skipped viewer, no chosen id (#295 I28).</summary>
    private sealed class SkippedOnRelaunch(FakeLauncher inner) : ILauncher
    {
        public async ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken)
        {
            var first = inner.Requests.All(r => r.Launch != request.Launch);
            var result = await inner.Launch(request, cancellationToken);
            return first || result is not StartedLaunch started ? result : started with { Viewer = ViewerOutcome.Skipped, ChosenSession = null };
        }

        public ValueTask<Result<Unit>> Stop(LaunchId launch, CancellationToken cancellationToken) => inner.Stop(launch, cancellationToken);

        public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken) => inner.Probe(cancellationToken);
    }

    /// <summary>A run diff whose first read of HEAD throws, as a worker that dies mid-activity would: the launch activity fails once and is retried.</summary>
    private sealed class FailsOnce(FakeRunDiff inner) : IRunDiff
    {
        private int _reads;

        public ValueTask<Result<string>> Head(string worktree, CancellationToken cancellationToken) =>
            Interlocked.Increment(ref _reads) == 1 ? throw new InvalidOperationException("the worker died") : inner.Head(worktree, cancellationToken);

        public ValueTask<Result<bool>> IsEmpty(string worktree, string startHead, CancellationToken cancellationToken) => inner.IsEmpty(worktree, startHead, cancellationToken);
    }

    private sealed record ItemInfo(string Key, string Id, string Repo);

    /// <summary>A running dev server (or the time-skipping one), the composed host with the fakes and a worker for the test body.</summary>
    private sealed class AgentRig : IAsyncDisposable
    {
        private readonly AgentRuntimeInterpreterTests _test;
        private readonly bool _owned;
        private InstanceStart? _start;

        private AgentRig(AgentRuntimeInterpreterTests test, WorkflowEnvironment environment, bool owned, ServiceProvider sindri)
        {
            _test = test;
            Environment = environment;
            _owned = owned;
            Sindri = sindri;
        }

        public WorkflowEnvironment Environment { get; }

        public ServiceProvider Sindri { get; }

        public LaunchId Launch => AgentLaunches.LaunchIdFor(Run());

        public string Worktree => AgentLaunches.WorktreeFor(Run(), new AgentStepTarget(ItemId.From(_item.Id), AgentId.From("claude-code"), Repo));

        public static async Task<AgentRig> Start(AgentRuntimeInterpreterTests test, WorkflowEnvironment? given, CancellationToken ct)
        {
            test._harness.Replacing = services => Register(services, test);
            test._harness.Recipe(RecipeName, _recipe);
            foreach (var yaml in test._also)
            {
                _ = test._harness.Approve(yaml);
            }

            var environment = given ?? await WorkflowsHarness.StartDevServer(ct: ct);
            var sindri = test._harness.Sindri(test._harness.Replacing);
            await WorkflowsHarness.HoldLease(sindri, ct);
            await ApproveTarget(sindri, ct);
            var rig = new AgentRig(test, environment, owned: given is null, sindri);
            rig._start = await rig.Prepare(ct);
            test._diff.Add(rig.Worktree);
            return rig;
        }

        public TemporalWorker Worker() => new(Environment.Client, Sindri.GetRequiredService<TemporalWorkerOptions>());

        public async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Start() => await WorkflowsHarness.Start(Environment.Client, _start!);   // why: Start(rig) set it

        public async Task Run(Func<WorkflowHandle<RecipeWorkflow, RecipeResult>, Task> body)
        {
            var ct = TestContext.Current.CancellationToken;
            using var worker = Worker();
            await worker.ExecuteAsync(async () =>
            {
                var handle = await Start();
                await body(handle);
                await Replayed(handle);
                if ((await handle.DescribeAsync()).Status == Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Running)
                {
                    await handle.TerminateAsync("test over");
                }
            }, ct);
        }

        public static async Task Replayed(WorkflowHandle<RecipeWorkflow, RecipeResult> handle)
        {
            var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(await handle.FetchHistoryAsync(), throwOnReplayFailure: false, TestContext.Current.CancellationToken);
            Assert.Null(replayed.ReplayFailure);
        }

        /// <summary>The session id of each 'launch.started' row, in order (null when the row has none).</summary>
        public async Task<List<string?>> StartedSessions(CancellationToken ct)
        {
            var rows = (await Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 200, ct)).Value;
            var sessions = new List<string?>();
            foreach (var row in rows.Where(static e => e.Envelope.Type == "launch.started"))
            {
                using var payload = JsonDocument.Parse(row.Envelope.Payload);
                sessions.Add(payload.RootElement.TryGetProperty("sessionId", out var id) ? id.GetString() : null);
            }

            return sessions;
        }

        /// <summary>The reason of the instance's one 'step.failed' row.</summary>
        public async Task<string?> FailureReason(CancellationToken ct)
        {
            var rows = (await Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 200, ct)).Value;
            foreach (var row in rows.Where(static e => e.Envelope.Type == "workflow.transition"))
            {
                using var payload = JsonDocument.Parse(row.Envelope.Payload);
                if (payload.RootElement.TryGetProperty("trigger", out var trigger) && trigger.GetString() == "step.failed")
                {
                    return payload.RootElement.GetProperty("detail").GetProperty("reason").GetString();
                }
            }

            return null;
        }

        public async ValueTask DisposeAsync()
        {
            await Sindri.DisposeAsync();
            if (_owned)
            {
                await Environment.ShutdownAsync();
            }
        }

        private AgentStepRun Run()
        {
            var address = _start!.WorkflowId();   // why: Start(rig) set it before the first use
            return new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.For(1, 1), new StepRef("write", "step.report.write"), WriteScope: false);
        }

        private async Task<InstanceStart> Prepare(CancellationToken ct)
        {
            var start = await WorkflowsHarness.RecipeStart(Sindri, _test._root, _item.Key, ct);
            var item = _test._harness.Fact(JsonSerializer.Serialize(new { id = _item.Id, repo = _item.Repo, spec = (string?)null }));
            var inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:item")] = new InputFact("item.json", item),
                [FactKey.From("fact:issue")] = new InputFact("issue.txt", _test._harness.Fact("The button is broken")),
            };
            return start with { Inputs = inputs };
        }

        private static async Task ApproveTarget(IServiceProvider sindri, CancellationToken ct)
        {
            var declared = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Repos: [new RepoEntry(_item.Repo, Repo)],
                Workflow: new WorkflowSettings(Agent: new WorkflowAgentSettings(AgentId.From("claude-code"))));
            var profile = ProfileRules.Check(declared).Value;
            var saved = await sindri.GetRequiredService<IApprovals>().Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), ct);
            Assert.False(saved.IsError, saved.IsError ? saved.Error.Message : null);
        }

        private static void Register(IServiceCollection services, AgentRuntimeInterpreterTests test)
        {
            _ = services.Replace(ServiceDescriptor.Singleton(test._launcherSeen ?? test._launcher));
            _ = services.Replace(ServiceDescriptor.Singleton<IAgentSessions>(test._sessions));
            _ = services.Replace(ServiceDescriptor.Singleton(test._diffSeen ?? test._diff));
            _ = services.Replace(ServiceDescriptor.Singleton(new AgentRuntimeOptions(PollInterval: TimeSpan.FromMilliseconds(100))));
        }
    }

    private static readonly string _recipe = string.Join('\n',
        "schema: 1",
        "id: workflow.dev-agent",
        "version: 1",
        "description: \"A Step an agent session runs (engine P8 interpreter tests).\"",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "  - { key: fact:issue, label: \"Issue\", type: text, kind: text }",
        "initial: write",
        "states:",
        "  write:",
        "    kind: active",
        "    step: step.report.write",
        "    budget: { tokens: 1000 }",
        "    timeout: { after: 1m, then: park }",
        "    next: \"Write up the resolution for {item}.\"",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: write, to: done, on: step.completed, when: { outcome: done }, produces: [fact:resolution] }");

    private static readonly string _parentRecipe = string.Join('\n',
        "schema: 1",
        "id: workflow.dev-agent-parent",
        "version: 1",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "  - { key: fact:issue, label: \"Issue\", type: text, kind: text }",
        "initial: write",
        "states:",
        "  write:",
        "    kind: active",
        "    step: step.report.write",
        "    budget: { tokens: 1000 }",
        "    timeout: { after: 1m, then: park }",
        "    next: \"Write up the resolution for {item}.\"",
        "  run: { kind: wait, timeout: { after: 2d, then: park },",
        "         waitFor: { child: { recipe: workflow.t-child, inputs: { \"fact:request\": { fact: fact:issue } }, onParked: propagate } },",
        "         next: \"Waiting for {child}.\" }",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: write, to: run, on: step.completed, when: { outcome: done }, produces: [fact:resolution] }",
        "  - { from: run, to: done, on: wait.satisfied, when: { outcome: resolved } }");
}
