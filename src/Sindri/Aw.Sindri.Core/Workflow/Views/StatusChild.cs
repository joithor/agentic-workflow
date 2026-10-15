using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The child a park mirrors (E-23).</summary>
/// <param name="Address">The child address.</param>
/// <param name="State">Its state.</param>
/// <param name="Reason">Its reason.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusChild(
    string Address,
    string State,
    string? Reason);
