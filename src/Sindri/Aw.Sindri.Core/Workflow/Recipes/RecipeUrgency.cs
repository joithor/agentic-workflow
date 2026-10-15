using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>How urgently a recipe's instances are listed (WF E-21): the order of the Home strip.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecipeUrgency>))]
public enum RecipeUrgency
{
    /// <summary>Listed first.</summary>
    [JsonStringEnumMemberName("incident")]
    Incident = 1,

    /// <summary>Listed second.</summary>
    [JsonStringEnumMemberName("triage")]
    Triage = 2,

    /// <summary>Listed last.</summary>
    [JsonStringEnumMemberName("work")]
    Work = 3,
}
