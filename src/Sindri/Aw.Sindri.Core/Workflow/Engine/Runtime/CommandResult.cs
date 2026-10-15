namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// What an accepted command Update returns (engine spec rev 8 §5.2, §6.3): the instance after it, and what happened in
/// the words the CLI prints ('-> checks', 'refused: re-running produce (round 2/3)', 'parked: rounds'). Temporal keeps
/// it as the Update's outcome, so a repeated nonce returns it again ('already applied').
/// </summary>
/// <param name="Instance">The instance after the command.</param>
/// <param name="Happened">What the command did.</param>
public sealed record CommandResult(InstanceView Instance, string Happened);
