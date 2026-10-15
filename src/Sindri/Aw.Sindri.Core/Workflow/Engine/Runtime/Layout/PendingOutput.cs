namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>A pending output of an open desk state (E-20).</summary>
/// <param name="Required">Whether the output is required.</param>
/// <param name="Checks">The checks it must pass.</param>
/// <param name="CompleteAction">The action that completes it.</param>
public sealed record PendingOutput(
    bool Required,
    IReadOnlyList<string> Checks,
    string CompleteAction);
