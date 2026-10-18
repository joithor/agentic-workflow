using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>The limits of the five size classes, all of them present.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SizeLimits
{
    /// <summary>Extra small.</summary>
    [JsonPropertyName("XS")]
    public required SizeLimit ExtraSmall { get; init; }

    /// <summary>Small.</summary>
    [JsonPropertyName("S")]
    public required SizeLimit Small { get; init; }

    /// <summary>Medium.</summary>
    [JsonPropertyName("M")]
    public required SizeLimit Medium { get; init; }

    /// <summary>Large.</summary>
    [JsonPropertyName("L")]
    public required SizeLimit Large { get; init; }

    /// <summary>Extra large.</summary>
    [JsonPropertyName("XL")]
    public required SizeLimit ExtraLarge { get; init; }

    /// <summary>The limits of <paramref name="size"/>.</summary>
    /// <param name="size">A size class.</param>
    /// <returns>Its limits.</returns>
    public SizeLimit Of(SizeClass size) => size switch
    {
        SizeClass.ExtraSmall => ExtraSmall,
        SizeClass.Small => Small,
        SizeClass.Medium => Medium,
        SizeClass.Large => Large,
        SizeClass.ExtraLarge => ExtraLarge,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Not a size class."),
    };

    internal IEnumerable<SizeLimit> All() => [ExtraSmall, Small, Medium, Large, ExtraLarge];
}
