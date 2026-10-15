using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>Whether a fact row is an input or an output of the recipe.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactDirection>))]
public enum FactDirection
{
    /// <summary>Supplied at the start.</summary>
    [JsonStringEnumMemberName("input")]
    Input = 1,

    /// <summary>Produced by a state.</summary>
    [JsonStringEnumMemberName("output")]
    Output = 2,
}
