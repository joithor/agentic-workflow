namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The instance-wide token budget (E-23).</summary>
/// <param name="Used">Tokens used.</param>
/// <param name="Limit">The cap, when declared.</param>
public sealed record InstanceBudget(
    long Used,
    long? Limit);
