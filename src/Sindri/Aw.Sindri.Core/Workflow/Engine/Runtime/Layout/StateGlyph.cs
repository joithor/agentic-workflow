using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>How a state shows on the band (UI screens rev 8 E-20): the app draws done, active and pending.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StateGlyph>))]
public enum StateGlyph
{
    /// <summary>A state the instance has finished, or a terminal state once reached.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>The state the instance is in.</summary>
    [JsonStringEnumMemberName("active")]
    Active,

    /// <summary>A state not reached yet.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,
}
