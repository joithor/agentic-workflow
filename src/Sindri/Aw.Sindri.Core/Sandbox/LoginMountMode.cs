using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>How a login mount is bound.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoginMountMode>))]
public enum LoginMountMode
{
    /// <summary>Read-only.</summary>
    [JsonStringEnumMemberName("ro")]
    ReadOnly,

    /// <summary>Read-write.</summary>
    [JsonStringEnumMemberName("rw")]
    ReadWrite,
}
