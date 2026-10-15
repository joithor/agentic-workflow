using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>How far a fact row has come.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactStatus>))]
public enum FactStatus
{
    /// <summary>The fact exists.</summary>
    [JsonStringEnumMemberName("produced")]
    Produced = 1,

    /// <summary>Its state is running.</summary>
    [JsonStringEnumMemberName("in-progress")]
    InProgress = 2,

    /// <summary>Not produced yet.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending = 3,
}
