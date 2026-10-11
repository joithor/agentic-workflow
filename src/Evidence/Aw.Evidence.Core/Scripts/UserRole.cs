using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>The app role a script signs in as.</summary>
[JsonConverter(typeof(StrictEnumConverter<UserRole>))]
public enum UserRole
{
    /// <summary>A manager.</summary>
    [JsonStringEnumMemberName("manager")]
    Manager,

    /// <summary>Staff.</summary>
    [JsonStringEnumMemberName("staff")]
    Staff,

    /// <summary>A central staffer.</summary>
    [JsonStringEnumMemberName("central-staffer")]
    CentralStaffer,

    /// <summary>A super admin.</summary>
    [JsonStringEnumMemberName("super-admin")]
    SuperAdmin,
}
