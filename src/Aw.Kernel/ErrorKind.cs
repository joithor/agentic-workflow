using System.Text.Json.Serialization;

namespace Aw.Kernel;

/// <summary>The kinds of expected failure (spec §4b.3). Serialized by these exact camelCase names.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ErrorKind>))]
public enum ErrorKind
{
    /// <summary>The input failed validation.</summary>
    [JsonStringEnumMemberName("invalid")]
    Invalid = 1,

    /// <summary>The thing asked for does not exist.</summary>
    [JsonStringEnumMemberName("notFound")]
    NotFound = 2,

    /// <summary>The operation conflicts with the current state.</summary>
    [JsonStringEnumMemberName("conflict")]
    Conflict = 3,

    /// <summary>A rate limit was hit; retrying later may succeed.</summary>
    [JsonStringEnumMemberName("rateLimited")]
    RateLimited = 4,

    /// <summary>A token, time or money budget is spent.</summary>
    [JsonStringEnumMemberName("budgetExceeded")]
    BudgetExceeded = 5,

    /// <summary>A dependency (a CLI, a database, a model provider) is not available.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable = 6,

    /// <summary>The operation took longer than allowed.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout = 7,

    /// <summary>A policy or gate refused the operation.</summary>
    [JsonStringEnumMemberName("forbidden")]
    Forbidden = 8,
}
