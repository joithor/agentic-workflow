namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>What a takeover paused (E-23).</summary>
/// <param name="TimeoutLeftSeconds">Whole seconds the state's timeout had left.</param>
public sealed record PausedLine(
    long TimeoutLeftSeconds);
