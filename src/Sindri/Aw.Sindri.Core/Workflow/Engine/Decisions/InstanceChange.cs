using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>One instance a decision changes (spec W13): its row after the Commit and its one event.</summary>
/// <param name="After">The row after the Commit.</param>
/// <param name="Event">Its event.</param>
public sealed record InstanceChange(WorkflowInstance After, WorkflowEvent Event);
