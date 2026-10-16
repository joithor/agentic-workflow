using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>Which side of the change a data-evidence run was at; the caller checks out and starts the app, the command only records it.</summary>
[JsonConverter(typeof(StrictEnumConverter<DataRunAt>))]
public enum DataRunAt
{
    /// <summary>The base (before) side.</summary>
    [JsonStringEnumMemberName("base")]
    Base,

    /// <summary>The head (after) side.</summary>
    [JsonStringEnumMemberName("head")]
    Head,
}
