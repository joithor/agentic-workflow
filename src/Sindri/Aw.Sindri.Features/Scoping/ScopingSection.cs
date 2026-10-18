using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>One section of the view.</summary>
/// <param name="Order">1 to 9.</param>
/// <param name="Name">The section name: checklist, flagged, excluded, acceptance, questions, answers, challenge, rounds or map.</param>
/// <param name="Title">The heading.</param>
/// <param name="State">Ready, empty (says so in <paramref name="Note"/>) or not yet (names the state that writes it).</param>
/// <param name="Note">The empty or not-yet text, or null.</param>
/// <param name="Rows">The rows.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopingSection(int Order, string Name, string Title, ScopingSectionState State, string? Note, IReadOnlyList<ScopingRow> Rows);
