using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A gate on a state (E-20).</summary>
/// <param name="Id">The gate block id.</param>
/// <param name="On">Entry or exit.</param>
/// <param name="Threshold">The threshold.</param>
/// <param name="Description">What it checks.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GateLine(
    BlockId Id,
    string On,
    double? Threshold,
    string Description);
