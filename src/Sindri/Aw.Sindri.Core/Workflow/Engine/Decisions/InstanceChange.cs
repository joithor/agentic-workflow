using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>One instance a decision changes (spec §5.6): the instance after it and its one ledger event.</summary>
/// <param name="After">The instance after the decision.</param>
/// <param name="Event">Its event.</param>
public sealed record InstanceChange(WorkflowInstance After, WorkflowEvent Event);
