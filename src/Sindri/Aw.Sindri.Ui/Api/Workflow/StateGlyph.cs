using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A state's progress glyph.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StateGlyph>))]
internal enum StateGlyph
{
    [JsonStringEnumMemberName("done")]
    Done = 1,

    [JsonStringEnumMemberName("active")]
    Active = 2,

    [JsonStringEnumMemberName("pending")]
    Pending = 3,
}
