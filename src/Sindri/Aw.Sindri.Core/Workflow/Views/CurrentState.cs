using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The expanded state (E-20).</summary>
/// <param name="State">The state id.</param>
/// <param name="Step">The Step it runs.</param>
/// <param name="Gates">Its gates.</param>
/// <param name="Desk">The desk form, when open.</param>
/// <param name="Status">The status layout as fields.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CurrentState(
    string State,
    StepDeclaration? Step,
    IReadOnlyList<GateLine> Gates,
    DeskForm? Desk,
    StatusFields Status);
