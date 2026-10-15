namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>Progress of a children state or an evidence fact, '4/9' (E-20).</summary>
/// <param name="Done">How many are done.</param>
/// <param name="Total">How many there are.</param>
public sealed record ChildProgress(
    int Done,
    int Total);
