using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>The HTTP methods a request step may use (upper case, as scripts write them).</summary>
[JsonConverter(typeof(StrictEnumConverter<HttpVerb>))]
public enum HttpVerb
{
    /// <summary>GET.</summary>
    [JsonStringEnumMemberName("GET")]
    Get,

    /// <summary>HEAD.</summary>
    [JsonStringEnumMemberName("HEAD")]
    Head,

    /// <summary>POST.</summary>
    [JsonStringEnumMemberName("POST")]
    Post,

    /// <summary>PUT.</summary>
    [JsonStringEnumMemberName("PUT")]
    Put,

    /// <summary>PATCH.</summary>
    [JsonStringEnumMemberName("PATCH")]
    Patch,

    /// <summary>DELETE.</summary>
    [JsonStringEnumMemberName("DELETE")]
    Delete,

    /// <summary>OPTIONS.</summary>
    [JsonStringEnumMemberName("OPTIONS")]
    Options,
}
