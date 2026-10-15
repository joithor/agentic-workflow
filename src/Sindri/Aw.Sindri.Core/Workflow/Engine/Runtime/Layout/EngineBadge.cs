namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The badge on the state an instance is interrupted in (E-20).</summary>
/// <param name="Kind">What interrupted it.</param>
/// <param name="Reason">Why.</param>
public sealed record EngineBadge(
    BadgeKind Kind,
    string Reason);
