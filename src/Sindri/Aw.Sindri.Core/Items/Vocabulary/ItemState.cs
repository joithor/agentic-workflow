using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Items.Vocabulary;

/// <summary>An item's state (API spec §3.6); written in kebab case ('needs-decision').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ItemState>))]
public enum ItemState
{
    /// <summary>Created and not yet queued or run.</summary>
    [JsonStringEnumMemberName("new")]
    New,

    /// <summary>Waiting for the engine to run it.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>An instance is running it.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>A pending decision points at it.</summary>
    [JsonStringEnumMemberName("needs-decision")]
    NeedsDecision,

    /// <summary>An open desk task waits for a human's answer.</summary>
    [JsonStringEnumMemberName("needs-answer")]
    NeedsAnswer,

    /// <summary>Its instance parked or escalated.</summary>
    [JsonStringEnumMemberName("parked")]
    Parked,

    /// <summary>A human took it over from the agent.</summary>
    [JsonStringEnumMemberName("taken-over")]
    TakenOver,

    /// <summary>Closed as done.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>Closed as cancelled.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
