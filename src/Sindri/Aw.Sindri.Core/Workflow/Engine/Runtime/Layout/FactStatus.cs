using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>How far a fact has come (E-20).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactStatus>))]
public enum FactStatus
{
    /// <summary>The fact exists.</summary>
    [JsonStringEnumMemberName("produced")]
    Produced,

    /// <summary>Its producer is running.</summary>
    [JsonStringEnumMemberName("in-progress")]
    InProgress,

    /// <summary>Nothing produced it yet.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,
}
