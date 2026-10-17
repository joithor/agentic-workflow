using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The status header line's parts (E-23).</summary>
/// <param name="Address">The address.</param>
/// <param name="Recipe">The instance's own recipe id.</param>
/// <param name="Hash12">The first 12 hash digits.</param>
/// <param name="Version">The instance's version, the one `--version` takes.</param>
/// <param name="State">The state.</param>
/// <param name="Detail">A detail.</param>
/// <param name="Since">Since.</param>
/// <param name="AgeSeconds">Age in seconds.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusHeader(
    InstanceAddress Address,
    RecipeId Recipe,
    string Hash12,
    int Version,
    string State,
    string? Detail,
    DateTimeOffset Since,
    long AgeSeconds);
