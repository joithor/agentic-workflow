using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Lint;

/// <summary>A deterministic sloppiness rule.</summary>
[JsonConverter(typeof(StrictEnumConverter<LintRule>))]
public enum LintRule
{
    /// <summary>Two elements' boxes overlap.</summary>
    [JsonStringEnumMemberName("overlap")]
    Overlap,

    /// <summary>Two left edges differ by 1 to 4 px.</summary>
    [JsonStringEnumMemberName("misaligned-edge")]
    MisalignedEdge,

    /// <summary>An element's right edge is past the viewport.</summary>
    [JsonStringEnumMemberName("phone-overflow")]
    PhoneOverflow,

    /// <summary>A long label is cut with an ellipsis.</summary>
    [JsonStringEnumMemberName("truncated-label")]
    TruncatedLabel,

    /// <summary>overflow:hidden with text clearly wider than the box.</summary>
    [JsonStringEnumMemberName("clipped")]
    Clipped,

    /// <summary>An interactive element without a hover or focus style.</summary>
    [JsonStringEnumMemberName("missing-hover-focus")]
    MissingHoverFocus,

    /// <summary>Text that is an unresolved placeholder (Loading…, undefined, null, NaN).</summary>
    [JsonStringEnumMemberName("unresolved-placeholder")]
    UnresolvedPlaceholder,

    /// <summary>A colour outside the allowed design-token values.</summary>
    [JsonStringEnumMemberName("non-token-color")]
    NonTokenColor,
}
