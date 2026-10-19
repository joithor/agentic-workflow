using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>Onyx's `UserGroupInfo` (`backend/onyx/server/models.py`): a group a user is in, by id and name.</summary>
/// <param name="Id">The group id.</param>
/// <param name="Name">The group name.</param>
internal sealed record OnyxGroupRefWire(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name);
