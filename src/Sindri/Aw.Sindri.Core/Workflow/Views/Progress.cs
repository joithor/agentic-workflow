using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A done-over-total count.</summary>
/// <param name="Done">Done.</param>
/// <param name="Total">Total.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Progress(
    int Done,
    int Total);
