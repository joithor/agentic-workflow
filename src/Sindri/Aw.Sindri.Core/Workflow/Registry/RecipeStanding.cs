using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>Where a registered recipe stands (spec §6.3 'recipe list').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecipeStanding>))]
public enum RecipeStanding
{
    /// <summary>Registered, never approved.</summary>
    [JsonStringEnumMemberName("unapproved")]
    Unapproved = 1,

    /// <summary>Approved and not revoked.</summary>
    [JsonStringEnumMemberName("approved")]
    Approved = 2,

    /// <summary>Revoked (directly or by a cascade).</summary>
    [JsonStringEnumMemberName("revoked")]
    Revoked = 3,
}
