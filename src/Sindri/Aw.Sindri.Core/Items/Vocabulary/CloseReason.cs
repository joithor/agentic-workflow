using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.Items.Vocabulary;

/// <summary>Why a human closes an item: the 'CloseItem' variant ('done' or 'cancelled').</summary>
[JsonConverter(typeof(StrictEnumConverter<CloseReason>))]
public enum CloseReason
{
    /// <summary>The work is done.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>The work is dropped.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
