using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A state's mark on the instance band (UI spec E-20): the app draws done, active and pending as a filled, half and empty circle.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StateGlyph>))]
public enum StateGlyph
{
    /// <summary>Finished; a terminal state is done once reached.</summary>
    [JsonStringEnumMemberName("done")]
    Done = 1,

    /// <summary>The instance is in this state.</summary>
    [JsonStringEnumMemberName("active")]
    Active = 2,

    /// <summary>Not reached yet.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending = 3,
}
