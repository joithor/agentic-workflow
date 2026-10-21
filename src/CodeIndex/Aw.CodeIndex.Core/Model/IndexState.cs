using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>What the index file says about itself (spec §7 Status: from the files only). Sindri overlays 'stale', 'building' and 'waiting' from the checkout and its jobs.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IndexState>))]
public enum IndexState
{
    /// <summary>No index for this repo yet.</summary>
    [JsonStringEnumMemberName("missing")]
    Missing,

    /// <summary>A readable index.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>The file or its pointer is unreadable; the next tick deletes and rebuilds it.</summary>
    [JsonStringEnumMemberName("damaged")]
    Damaged,
}
