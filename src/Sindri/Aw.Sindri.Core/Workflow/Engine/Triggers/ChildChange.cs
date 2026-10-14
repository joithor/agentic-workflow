using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>A child's change that moves its parent's suspension (spec §5.8). A child's end arrives as 'wait.satisfied' (<see cref="ChildWaitSource"/>), not as one of these.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChildChange>))]
public enum ChildChange
{
    /// <summary>The child parked: with 'onParked: propagate' the parent pushes a park mirroring its reason and question.</summary>
    [JsonStringEnumMemberName("parked")]
    Parked = 1,

    /// <summary>The child resumed: the parent's mirroring suspension ends, back to its wait (the same frame and child).</summary>
    [JsonStringEnumMemberName("resumed")]
    Resumed = 2,

    /// <summary>The child escalated: the parent pushes an 'escalated (child)' frame (always, whatever onParked says).</summary>
    [JsonStringEnumMemberName("escalated")]
    Escalated = 3,
}
