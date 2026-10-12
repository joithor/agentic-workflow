using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>Who runs a Step (spec §5.1, W20): the desk runtime until the agent runtime (§6.5, P8) runs it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StepRuntime>))]
public enum StepRuntime
{
    /// <summary>The human, reporting with 'sindri workflow complete --run &lt;token&gt;'; outcomes are labelled 'via: desk'.</summary>
    [JsonStringEnumMemberName("desk")]
    Desk = 1,

    /// <summary>An agent session launched for the run.</summary>
    [JsonStringEnumMemberName("agent")]
    Agent = 2,
}
