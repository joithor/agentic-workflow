using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The form of an open desk task (E-20).</summary>
/// <param name="Run">The open run's token, as complete takes it.</param>
/// <param name="Outputs">The outputs asked for.</param>
/// <param name="Checks">The checks run on complete.</param>
/// <param name="CompleteAction">The action id that completes it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeskForm(
    string Run,
    IReadOnlyList<DeskOutput> Outputs,
    IReadOnlyList<string> Checks,
    string CompleteAction);
