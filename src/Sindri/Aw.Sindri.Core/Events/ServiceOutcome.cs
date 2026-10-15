using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>How a background service's run ended; written as 'ok' or 'failed'.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ServiceOutcome>))]
public enum ServiceOutcome
{
    /// <summary>The run succeeded.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>The run failed: an error, an exception, or a service this build doesn't have.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
