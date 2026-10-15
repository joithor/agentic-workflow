namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The child a park mirrors (E-23).</summary>
/// <param name="Address">The child's address.</param>
/// <param name="State">The child's state.</param>
/// <param name="Reason">Why the child is parked.</param>
public sealed record ChildPark(
    string Address,
    string State,
    string? Reason);
