using Aw.Sindri.Core.Workflow.Engine.Decisions;

namespace Aw.Sindri.Core.Workflow.Engine.Store;

/// <summary>
/// One Commit (spec W13, §5.3): everything a decision writes, in one transaction: the recorded decision under its
/// key, the events and rows (the trigger's instance, and a child it creates), the outbox commands with their
/// 'workflow_commands' map rows, and the consumed nonce. Deadlines and the stack are part of each row.
/// </summary>
/// <param name="Decision">The decision recorded under the key.</param>
/// <param name="Writes">The rows and their events.</param>
/// <param name="Outbox">The outbox commands.</param>
/// <param name="Nonce">The command's nonce, consumed whether the command applied or was refused.</param>
public sealed record WorkflowCommit(RecordedDecision Decision, IReadOnlyList<InstanceWrite> Writes, IReadOnlyList<EffectCommand> Outbox, CommandNonce? Nonce);
