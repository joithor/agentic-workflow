using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>Who stands behind a node, edge or item: 'untrusted' unless the owner's own answer made it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LensTrust>))]
public enum LensTrust
{
    /// <summary>Text from outside; the default for everything a lens returned.</summary>
    [JsonStringEnumMemberName("untrusted")]
    Untrusted = 1,

    /// <summary>A decision record with the owner's answer provenance (scoping spec §11).</summary>
    [JsonStringEnumMemberName("owner")]
    Owner = 2,
}
