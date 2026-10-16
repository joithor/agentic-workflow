using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>What an api-run step does.</summary>
[JsonConverter(typeof(StrictEnumConverter<ApiAction>))]
public enum ApiAction
{
    /// <summary>An HTTP request.</summary>
    [JsonStringEnumMemberName("request")]
    Request,

    /// <summary>A CLI call.</summary>
    [JsonStringEnumMemberName("cli")]
    Cli,
}
