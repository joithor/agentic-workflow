using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A taken-over instance's paused line (E-23).</summary>
/// <param name="TimeoutLeftSeconds">Timeout left in seconds.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusPaused(
    long TimeoutLeftSeconds);
