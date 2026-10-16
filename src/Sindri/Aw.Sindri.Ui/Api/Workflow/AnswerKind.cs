using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>How a status answer is given.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnswerKind>))]
internal enum AnswerKind
{
    [JsonStringEnumMemberName("text")]
    Text = 1,

    [JsonStringEnumMemberName("resume")]
    Resume = 2,

    [JsonStringEnumMemberName("grant")]
    Grant = 3,

    [JsonStringEnumMemberName("grantTokens")]
    GrantTokens = 4,

    [JsonStringEnumMemberName("accept")]
    Accept = 5,

    [JsonStringEnumMemberName("cancel-only")]
    CancelOnly = 6,
}
