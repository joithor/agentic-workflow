using System.Text.Json.Serialization;

namespace Aw.Context.Core.Review;

/// <summary>Which part of the review queue an entry sits in (spec §8.7): deterministic proposals are accepted in bulk; model ones one by one, biggest impact first.</summary>
[JsonConverter(typeof(StrictEnumConverter<ReviewGroup>))]
public enum ReviewGroup
{
    /// <summary>Found by the extractor; accepted in one step.</summary>
    [JsonStringEnumMemberName("deterministic")]
    Deterministic,

    /// <summary>Proposed by a model (a cluster, a labeling pass, a scoping run), or a suggested link; needs a person.</summary>
    [JsonStringEnumMemberName("model")]
    Model,

    /// <summary>An override whose target no longer resolves: repoint or drop it.</summary>
    [JsonStringEnumMemberName("orphaned")]
    Orphaned,
}
