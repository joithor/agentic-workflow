using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>Where a cancel came from (spec §3.8).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CancelOrigin>))]
public enum CancelOrigin
{
    /// <summary>'workflow cancel' on this instance.</summary>
    [JsonStringEnumMemberName("command")]
    Command = 1,

    /// <summary>The item was cancelled in the tracker.</summary>
    [JsonStringEnumMemberName("tracker")]
    Tracker = 2,

    /// <summary>The parent was cancelled; every child is cancelled, whatever its state (§5.7).</summary>
    [JsonStringEnumMemberName("parent")]
    Parent = 3,
}
