using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>What a pending output of an open desk needs (E-20).</summary>
/// <param name="Required">Whether required.</param>
/// <param name="Checks">The checks.</param>
/// <param name="CompleteAction">The completing action id.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactPending(
    bool Required,
    IReadOnlyList<string> Checks,
    string CompleteAction);
