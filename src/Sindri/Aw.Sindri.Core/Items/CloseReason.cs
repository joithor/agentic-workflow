using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Items;

/// <summary>Why a human closes an item: the 'CloseItem' variant ('done' or 'cancelled').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CloseReason>))]
public enum CloseReason
{
    /// <summary>The work is done.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>The work is dropped.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
