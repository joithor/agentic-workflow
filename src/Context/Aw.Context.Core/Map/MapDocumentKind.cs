using System.Text.Json.Serialization;

namespace Aw.Context.Core.Map;

/// <summary>Where a map file came from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MapDocumentKind>))]
public enum MapDocumentKind
{
    /// <summary>Written by hand (YAML or JSON); provenance defaults to human.</summary>
    [JsonStringEnumMemberName("authored")]
    Authored,

    /// <summary>Written by 'context map export'.</summary>
    [JsonStringEnumMemberName("export")]
    Export,
}
