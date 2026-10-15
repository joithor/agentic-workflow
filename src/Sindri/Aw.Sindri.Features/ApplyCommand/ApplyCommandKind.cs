using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>Which command 'ApplyCommand' carries (engine spec §6.3): an answer, a recipe command, or a cancel. Takeover and handback are P5's.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApplyCommandKind>))]
public enum ApplyCommandKind
{
    /// <summary>'workflow answer', in its variants.</summary>
    [JsonStringEnumMemberName("answer")]
    Answer = 1,

    /// <summary>'workflow command &lt;verb&gt;': the recipe's 'command.&lt;verb&gt;' trigger.</summary>
    [JsonStringEnumMemberName("command")]
    Command = 2,

    /// <summary>'workflow cancel'.</summary>
    [JsonStringEnumMemberName("cancel")]
    Cancel = 3,
}
