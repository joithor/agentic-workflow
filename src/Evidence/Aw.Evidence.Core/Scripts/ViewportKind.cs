using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>A named viewport a script runs in (sizes: <see cref="ScriptViewports"/>).</summary>
[JsonConverter(typeof(StrictEnumConverter<ViewportKind>))]
public enum ViewportKind
{
    /// <summary>Desktop, 1280x800.</summary>
    [JsonStringEnumMemberName("desktop")]
    Desktop,

    /// <summary>Phone, 375x812.</summary>
    [JsonStringEnumMemberName("phone")]
    Phone,
}
