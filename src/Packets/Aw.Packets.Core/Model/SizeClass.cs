using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>A task's size class (addendum §17.4 "every task fits its size class's budget").</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SizeClass>))]
public enum SizeClass
{
    /// <summary>Extra small.</summary>
    [JsonStringEnumMemberName("XS")]
    ExtraSmall = 1,

    /// <summary>Small.</summary>
    [JsonStringEnumMemberName("S")]
    Small = 2,

    /// <summary>Medium.</summary>
    [JsonStringEnumMemberName("M")]
    Medium = 3,

    /// <summary>Large.</summary>
    [JsonStringEnumMemberName("L")]
    Large = 4,

    /// <summary>Extra large.</summary>
    [JsonStringEnumMemberName("XL")]
    ExtraLarge = 5,
}
