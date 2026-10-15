using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The counts on the frame's tabs (E-20); context is null until the context lenses are served.</summary>
/// <param name="Timeline">Timeline events.</param>
/// <param name="Decisions">Decisions.</param>
/// <param name="Requests">Requests.</param>
/// <param name="Context">Context items.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ViewCounts(
    int Timeline,
    int Decisions,
    int Requests,
    int? Context);
