using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>A band of the context map a found node sits in (the Context view draws each in its own band).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LensBand>))]
public enum LensBand
{
    /// <summary>Code nodes and 'code:&lt;path&gt;' file keys.</summary>
    [JsonStringEnumMemberName("code")]
    Code = 1,

    /// <summary>Product nodes.</summary>
    [JsonStringEnumMemberName("product")]
    Product = 2,

    /// <summary>Intent: decisions, tickets and the like.</summary>
    [JsonStringEnumMemberName("intent")]
    Intent = 3,
}
