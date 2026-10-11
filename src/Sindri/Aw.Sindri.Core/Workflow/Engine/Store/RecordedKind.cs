using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Store;

/// <summary>What a recorded key decided (spec W12).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecordedKind>))]
public enum RecordedKind
{
    /// <summary>Applied: a replay prints 'already applied' and exits 0.</summary>
    [JsonStringEnumMemberName("applied")]
    Applied = 1,

    /// <summary>Refused by a gate or evidence: a replay prints the recorded refusal and exits 2.</summary>
    [JsonStringEnumMemberName("refused")]
    Refused = 2,

    /// <summary>Absorbed without a change: a replay prints the reason and exits 0.</summary>
    [JsonStringEnumMemberName("absorbed")]
    Absorbed = 3,
}
