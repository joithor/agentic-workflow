namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>A frame's open Step run (spec §5.1): only a report with its token is current; a pop keeps it (W16).</summary>
/// <param name="Token">'{entry}.{n}'.</param>
/// <param name="Runtime">Desk or agent.</param>
/// <param name="Launch">The agent launch ('LCH-…'), for an agent run.</param>
/// <param name="Task">The desk task the human sees, for a desk run.</param>
public sealed record OpenRun(RunToken Token, StepRuntime Runtime, string? Launch, string? Task);
