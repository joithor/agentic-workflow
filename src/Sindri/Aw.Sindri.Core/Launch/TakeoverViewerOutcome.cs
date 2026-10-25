using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>What opening a taken-over Step's session in a viewer came to (issue 1004): the 'outcome' of 'launch.takeover-opened' and of a takeover's 'viewer'.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TakeoverViewerOutcome>))]
public enum TakeoverViewerOutcome
{
    /// <summary>A client attached inside the observation window.</summary>
    [JsonStringEnumMemberName("opened")]
    Opened,

    /// <summary>The viewer ran without error but no client has attached yet.</summary>
    [JsonStringEnumMemberName("unconfirmed")]
    Unconfirmed,

    /// <summary>The viewer failed; the Step is taken over all the same and the attach command is the way in.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>No viewer on this box can open a window (the print viewer, or none detected); the attach command is the way in.</summary>
    [JsonStringEnumMemberName("none-usable")]
    NoneUsable,
}
