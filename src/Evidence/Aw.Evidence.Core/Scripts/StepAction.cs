using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>What a script step does.</summary>
[JsonConverter(typeof(StrictEnumConverter<StepAction>))]
public enum StepAction
{
    /// <summary>Navigate to a path on the host.</summary>
    [JsonStringEnumMemberName("goto")]
    Goto,

    /// <summary>Click the element with the target test id.</summary>
    [JsonStringEnumMemberName("click")]
    Click,

    /// <summary>Fill the element with the target test id.</summary>
    [JsonStringEnumMemberName("fill")]
    Fill,

    /// <summary>Wait for the target text to be visible.</summary>
    [JsonStringEnumMemberName("expect-visible")]
    ExpectVisible,
}
