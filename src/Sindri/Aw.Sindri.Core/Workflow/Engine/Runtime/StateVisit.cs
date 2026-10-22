namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The latest visit of an instance to one state: when it was entered and, once it moved on, when it left.</summary>
/// <param name="EnteredAt">When the instance entered the state (the workflow's clock).</param>
/// <param name="LeftAt">When it left; null while the state is current.</param>
public sealed record StateVisit(DateTimeOffset EnteredAt, DateTimeOffset? LeftAt = null);
