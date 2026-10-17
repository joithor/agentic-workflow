using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The block runtime in the interpreter (DT spec §8.2): the dev evidence route's judge state runs a registered block Step in an activity, once, and a pre-DT-J history replays on the desk path.</summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class BlockRuntimeInterpreterTests : IDisposable
{
    private const string Route = "workflow.dev-evidence-route";
    private static readonly BlockId _judge = BlockId.From("step.judge.choose");
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task A_block_step_completes_without_a_desk_report()
    {
        var fake = FakeBlockStep.Completing("ui");
        var ct = TestContext.Current.CancellationToken;
        await Run(fake, maxDuration: null, async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.State == "evidence-ui");

            Assert.Equal(("evidence-ui", 1), (view.State, fake.Runs));
            Assert.NotNull(view.Task);   // the next state's desk task
            Assert.Equal("fact:diff", fake.Last!.Facts.Keys.Select(static k => k.Value).Order(StringComparer.Ordinal).Last());
            await Task.Delay(1, ct);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_block_step_past_MaxDuration_is_step_failed_timeout_and_goes_to_the_desk_twin()
    {
        var fake = FakeBlockStep.Hanging();
        var ct = TestContext.Current.CancellationToken;
        await Run(fake, TimeSpan.FromSeconds(2), async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.State == "route-evidence-desk");

            Assert.NotNull(view.Task);
            Assert.Equal(1, fake.Runs);
            await Task.Delay(1, ct);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task The_block_activity_is_attempted_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = new FakeBlockStep(static (_, _) => throw new InvalidOperationException("the judge fell over"));
        await Run(fake, maxDuration: null, async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "route-evidence-desk");
            await Task.Delay(TimeSpan.FromSeconds(2), ct);   // a retry would be a second run by now

            Assert.Equal(1, fake.Runs);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Cancel_while_a_block_step_runs_cancels_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.Hanging();
        await Run(fake, maxDuration: null, async handle =>
        {
            await WaitFor(() => fake.Runs > 0, ct);
            var stamp = new CommandStamp(CommandNonce.From(new string('c', 32)), new string('1', 64));
            var cancelled = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(new CancelRequested("human:cli", CancelOrigin.Command, stamp, Sequence: null, Parent: null)), new WorkflowUpdateOptions(new string('c', 32)));

            Assert.Equal("cancelled", cancelled.Instance.State);
            await WaitFor(() => fake.Cancelled == 1, ct);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_pre_DT_J_history_replays_on_the_desk_path()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("histories/dev-route-desk.json")!;
        using var reader = new StreamReader(stream);
        var history = Temporalio.Common.WorkflowHistory.FromJson("T-1/workflow.dev-evidence-route/1", await reader.ReadToEndAsync(ct));

        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(history, throwOnReplayFailure: false, ct);

        Assert.Null(replayed.ReplayFailure);
    }

    private async Task Run(FakeBlockStep fake, TimeSpan? maxDuration, Func<WorkflowHandle<RecipeWorkflow, RecipeResult>, Task> body)
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Replacing = services => Register(services, fake, maxDuration);
        _harness.Recipe(Route, WorkflowsHarness.Shipped("dev/workflow.dev-evidence-route.yaml"));
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var sindri = _harness.Sindri(_harness.Replacing);
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, Route, "T-1", ct);
            var inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:change")] = new InputFact("change.txt", _harness.Fact("Add a button to the settings page")),
                [FactKey.From("fact:diff")] = new InputFact("diff.txt", _harness.Fact("https://example.test/pr/1")),
            };
            var handle = await WorkflowsHarness.Start(env.Client, start with { Inputs = inputs });
            await body(handle);
            var hist = await handle.FetchHistoryAsync();
            var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(hist, throwOnReplayFailure: false, ct);
            Assert.Null(replayed.ReplayFailure);
            if ((await handle.DescribeAsync()).Status == Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Running)
            {
                await handle.TerminateAsync("test over");
            }
        }, ct);
    }

    private static void Register(IServiceCollection services, FakeBlockStep fake, TimeSpan? maxDuration)
    {
        _ = services.Replace(ServiceDescriptor.Singleton(new BlockStepRuntimes([new BlockStepRegistration(_judge)])));
        _ = services.AddKeyedSingleton<IBlockStep>(_judge.Value, fake);
        if (maxDuration is { } max)
        {
            _ = services.Replace(ServiceDescriptor.Singleton(provider => new BlockCatalog(
                provider.GetServices<BlockDescriptor>().Select(d => d.Id == _judge ? d with { MaxDuration = max } : d),
                provider.GetServices<EventDescriptor>())));
        }
    }

    private static async Task WaitFor(Func<bool> condition, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.True(condition());
    }
}
