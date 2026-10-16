using System.Text.Json.Serialization;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.Lanes;

/// <summary>The lane map after a refresh.</summary>
/// <param name="Version">The map version; it rises whenever the assignments change.</param>
/// <param name="Changed">Whether this refresh changed the assignments.</param>
/// <param name="Entries">The assignments, in lane order.</param>
/// <param name="Unmapped">Configured lanes with no live session.</param>
/// <param name="Standing">How each non-Claude lane stands for the meta's obligations; absent (null) for a team of Claude lanes.</param>
/// <param name="Announcement">The text to send each lane, starting "LANE MAP v&lt;n&gt;:".</param>
public sealed record LanesResponse(int Version, bool Changed, IReadOnlyList<LaneAssignment> Entries, IReadOnlyList<string> Unmapped, string Announcement, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<LaneHealth>? Standing = null);
