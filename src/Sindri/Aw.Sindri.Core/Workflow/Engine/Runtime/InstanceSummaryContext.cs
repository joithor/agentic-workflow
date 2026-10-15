namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a list row needs besides the instance view.</summary>
/// <param name="NotStarted">True for a child that has not run yet (its chip reads QUEUED).</param>
public sealed record InstanceSummaryContext(bool NotStarted = false);
