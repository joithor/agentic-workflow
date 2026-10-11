using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// A pop (spec §5.1, W16): the instance with the frame restored exactly (the same entry, run token, child and
/// pending inbox rows, deadlines re-armed with arm + 1), and what to run next. When the popped frame carried a
/// pending park, that park takes effect instead of the resume action.
/// </summary>
/// <param name="Instance">The instance after the pop.</param>
/// <param name="Resume">The popped frame's resume action.</param>
/// <param name="Pending">The pending park that takes effect instead.</param>
public sealed record PopResult(WorkflowInstance Instance, ResumeAction Resume, PendingPark? Pending);
