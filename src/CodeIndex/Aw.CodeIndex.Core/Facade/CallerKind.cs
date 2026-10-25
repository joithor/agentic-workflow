using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>Who calls the facade.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CallerKind>))]
public enum CallerKind
{
    /// <summary>The owner.</summary>
    [JsonStringEnumMemberName("human")]
    Human,

    /// <summary>A Sindri service.</summary>
    [JsonStringEnumMemberName("service")]
    Service,

    /// <summary>An agent session, scoped to its own repo.</summary>
    [JsonStringEnumMemberName("session")]
    Session,
}
