using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>One user group of `GET /api/manage/admin/user-group`.</summary>
/// <param name="Id">The group id.</param>
/// <param name="Name">The group name (the default admin group is named "Admin").</param>
/// <param name="IsDefault">Whether Onyx made it itself.</param>
internal sealed record OnyxGroupWire(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("is_default")] bool IsDefault);
