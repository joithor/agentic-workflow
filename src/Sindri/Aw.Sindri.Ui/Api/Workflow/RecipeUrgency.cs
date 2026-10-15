using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A recipe's listing urgency.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecipeUrgency>))]
internal enum RecipeUrgency
{
    [JsonStringEnumMemberName("incident")]
    Incident = 1,

    [JsonStringEnumMemberName("triage")]
    Triage = 2,

    [JsonStringEnumMemberName("work")]
    Work = 3,
}
