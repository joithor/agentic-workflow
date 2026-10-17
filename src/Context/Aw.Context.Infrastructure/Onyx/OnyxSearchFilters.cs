using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// Onyx's BaseFilters (onyx/context/search/models.py:104): empty unless a query names an update time, because Sindri wants recall over every connector
/// the key can see and Onyx's only time filter keeps documents updated since a time, which isn't an as-of cut.
/// </summary>
/// <param name="TimeCutoff">Keep documents updated at or after this time, or null for no filter.</param>
internal sealed record OnyxSearchFilters(
    [property: JsonPropertyName("time_cutoff"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? TimeCutoff = null);
