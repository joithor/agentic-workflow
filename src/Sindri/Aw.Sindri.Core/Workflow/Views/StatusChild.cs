using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The child a park mirrors (E-23).</summary>
/// <param name="Address">The child address.</param>
/// <param name="State">Its state.</param>
/// <param name="Reason">Its reason.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusChild(
    InstanceAddress Address,
    string State,
    string? Reason);
