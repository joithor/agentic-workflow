using System.Text.Json.Serialization;

namespace Aw.Context.Core.Proposals;

/// <summary>Where a scoped surface stands against the map (spec §8.7).</summary>
[JsonConverter(typeof(StrictEnumConverter<GraphStatusKind>))]
public enum GraphStatusKind
{
    /// <summary>It is a node of the current map.</summary>
    [JsonStringEnumMemberName("mapped")]
    Mapped,

    /// <summary>It is proposed in the latest draft and not promoted yet.</summary>
    [JsonStringEnumMemberName("new")]
    New,

    /// <summary>It is in neither.</summary>
    [JsonStringEnumMemberName("unmapped")]
    Unmapped,
}
