using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>What a Commit writes (spec §5.2, §5.4, W13, W21): one event per changed instance with its full post-state delta, and the outbox commands.</summary>
public static partial class TransitionDecision
{
    /// <summary>
    /// The transition's effects as outbox commands (§5.4): each id a UUIDv8 (RFC 9562) over the SHA-256 of
    /// '{trigger key}:{effect index}', so each Commit gets ids of its own; each identity '{instance}:{entry}:{index}'
    /// with the entered frame's entry.
    /// </summary>
    private static List<EffectCommand> Effects(TriggerKey key, RecipeTransition transition, WorkflowInstanceId instance, int entry) =>
        [.. transition.Effects.Select((use, index) => new EffectCommand(
            CommandId(key, index), use.Use, new EffectIdentity(instance, entry, index), CommandOrigin.Recipe, use.With))];

    private static OutboxCommandId CommandId(TriggerKey key, int index)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(Encoding.UTF8.GetBytes($"{key.Value}:{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}"), digest);
        var bytes = digest[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return OutboxCommandId.For(new Guid(bytes, bigEndian: true));
    }

    private sealed partial record Deciding
    {
        /// <summary>The instance's one workflow.transition event, and nothing else.</summary>
        public Applied Commit(WorkflowInstance after) => Commit(after, Records.None);

        /// <summary>The instance's one workflow.transition event with the transition's gate and evidence records.</summary>
        public Applied Commit(WorkflowInstance after, Records records) =>
            new(Key, [Change(after, WorkflowEventType.Transition, records)], [], []);

        /// <summary>The commit, or <see cref="Absorbed"/> when the rule left the row as it was.</summary>
        public Decision CommitOrAbsorb(WorkflowInstance after, WorkflowInstance before, string reason) =>
            ReferenceEquals(after, before) ? new Absorbed(Key, reason) : Commit(after);

        /// <summary>The row after the Commit (version + 1) and its event: the trigger, the version step, the epoch, the records and the delta (W21).</summary>
        public InstanceChange Change(WorkflowInstance after, WorkflowEventType type, Records records)
        {
            var before = Input.Instance;
            var row = after with { Version = before.Version + 1 };
            var trigger = Answered is { } answer
                ? new TriggerRecord(answer.Name, Key, answer.Actor, Outcome: null)
                : new TriggerRecord(Trigger.Name, Key, Trigger.Actor, Outcome(Trigger));
            return new InstanceChange(row, new WorkflowEvent(
                type, row.Id, trigger, before.Version, row.Version, Context.Epoch, Started: null, Rules.Delta(before, row),
                records.Gates, records.Evidence, Grant: null, Override: null, Refusal: null, Outbox: []));
        }
    }
}
