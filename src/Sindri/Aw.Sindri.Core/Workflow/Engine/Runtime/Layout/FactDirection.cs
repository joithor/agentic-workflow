using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>Whether a fact is an input or an output of the recipe (E-20).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactDirection>))]
public enum FactDirection
{
    /// <summary>A declared input.</summary>
    [JsonStringEnumMemberName("input")]
    Input,

    /// <summary>A declared output.</summary>
    [JsonStringEnumMemberName("output")]
    Output,
}
