using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// 'RecordEffect' and 'RunEffect' (engine spec rev 8.1 §5.6, §5.7; engine P6 unit (a)) over the home's real ledger:
/// an effect's row is keyed like 'RecordEvent'; a run performs the effect through its block, writes 'outbox.dispatched'
/// and appends the block's reply caused by that row, from the reply event's producer. A re-emission (a 'retry', a resumed
/// dispatch park) carries a new command id under the same identity and is a no-op: one dispatched row, one reply, one
/// notice.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EffectActivitiesTests : IDisposable
{
    private static readonly InstanceAddress _address = InstanceAddress.Root(ItemKey.From("T-81"), RecipeId.From("workflow.dev-notify"), 1);
    private static readonly WorkflowInstanceId _instance = WorkflowInstanceId.From("WFI-" + new string('8', 32));
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");
    private readonly WorkflowsHarness _harness = new();
    private readonly FakeDeskEffect _desk = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task RecordEffect_writes_the_row_under_its_activity_key()
    {
        await using var sindri = _harness.Sindri(_desk.Replace);
        await WorkflowsHarness.HoldLease(sindri, Token);
        var identity = new EffectIdentity(_instance, Entry: 2, Index: 0).Text;
        var row = new EffectRow(identity, "cmd-1", _notify, CommandOrigin.Recipe, EffectState.Pending, _address);

        var appended = await Local("7").RunAsync(() => sindri.GetRequiredService<EffectActivities>().RecordAsync(new EffectRecord("T-81", row)));

        var recorded = Assert.Single(await EffectRows.Events(sindri, OutboxEvents.Pending));
        Assert.Equal((appended.Sequence, $"workflow:{_address.Value}:run-1:7", "T-81"), (recorded.Sequence, recorded.IdempotencyKey, recorded.Item));
        Assert.Equal(row, EffectRows.Payload<EffectRow>(sindri.GetRequiredService<EventFactory>(), recorded));
    }

    [Fact]
    public async Task RunEffect_again_under_the_same_identity_performs_and_replies_once()
    {
        await using var sindri = _harness.Sindri(_desk.Replace);
        await WorkflowsHarness.HoldLease(sindri, Token);
        var activities = sindri.GetRequiredService<EffectActivities>();
        var command = new EffectCommand(_notify, new EffectIdentity(_instance, Entry: 2, Index: 0), CommandOrigin.Recipe, new Dictionary<string, Core.Workflow.Recipes.WithValue>(StringComparer.Ordinal));
        var run = new EffectRun(_address.Value, "T-81", Attempt: 1, command, "cmd-1", Text: null, new Dictionary<FactKey, FactValue>());

        var first = await Regular("effect-1").RunAsync(() => activities.RunAsync(run));
        var again = await Regular("effect-2").RunAsync(() => activities.RunAsync(run with { CommandId = "cmd-2" }));

        var dispatched = Assert.Single(await EffectRows.Events(sindri, OutboxEvents.Dispatched));
        var reply = Assert.Single(await EffectRows.Events(sindri, "notify.delivered"));
        var row = EffectRows.Payload<EffectRow>(sindri.GetRequiredService<EventFactory>(), dispatched);
        Assert.Equal((false, "delivered", dispatched.Sequence), (first.Existed, first.Reply, first.Dispatched));
        Assert.Equal((true, dispatched.Sequence), (again.Existed, again.Dispatched));
        Assert.Equal((command.Identity.Text, "cmd-1", EffectState.Dispatched, _address), (row.Identity, row.Command, row.State, row.Instance));
        Assert.Equal(("system:dispatcher", dispatched.Envelope.Id, "T-81"), (reply.Envelope.Actor, reply.Envelope.CausationId, reply.Item));
        using (var payload = JsonDocument.Parse(reply.Envelope.Payload))
        {
            Assert.Equal("delivered", payload.RootElement.GetProperty("outcome").GetString());
        }

        Assert.Equal([command.Identity.Text], _desk.Performed);
    }

    private static ActivityEnvironment Local(string activityId) => new()
    {
        Info = ActivityEnvironment.DefaultInfo with { WorkflowId = _address.Value, WorkflowRunId = "run-1", ActivityId = activityId, IsLocal = true },
    };

    private static ActivityEnvironment Regular(string activityId) => new()
    {
        Info = ActivityEnvironment.DefaultInfo with { WorkflowId = _address.Value, WorkflowRunId = "run-1", ActivityId = activityId, IsLocal = false },
    };
}
