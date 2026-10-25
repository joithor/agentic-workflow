using System.Text.Json.Serialization;

namespace Aw.Context.Core.Code;

/// <summary>How a test file came to cover an implementing file: a stored edge, or one of the two lexical rules (S-CL). Edge outranks imports, imports outranks sibling.</summary>
[JsonConverter(typeof(StrictEnumConverter<CodeBasis>))]
public enum CodeBasis
{
    /// <summary>A 'tests' edge of the map.</summary>
    [JsonStringEnumMemberName("edge")]
    Edge,

    /// <summary>The test's text names the implementing file's path.</summary>
    [JsonStringEnumMemberName("imports")]
    Imports,

    /// <summary>The test's name, without its test marker, equals the implementing file's name.</summary>
    [JsonStringEnumMemberName("sibling")]
    Sibling,
}
