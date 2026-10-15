using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>What a state's timeout does when it runs out (E-23).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimeoutKind>))]
public enum TimeoutKind
{
    /// <summary>The instance parks.</summary>
    [JsonStringEnumMemberName("parks")]
    Parks,

    /// <summary>The instance escalates.</summary>
    [JsonStringEnumMemberName("escalates")]
    Escalates,
}
