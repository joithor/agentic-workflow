using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Store;

/// <summary>One row a Commit writes (spec §5.3 steps 7–8): the row after, the version it expects, and its event.</summary>
/// <param name="After">The row after the Commit.</param>
/// <param name="FromVersion">The version the row must still have ('UPDATE … WHERE version = @fromVersion'); 0 creates it.</param>
/// <param name="Event">Its event.</param>
public sealed record InstanceWrite(WorkflowInstance After, long FromVersion, WorkflowEvent Event);
