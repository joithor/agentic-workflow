using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>A planned API script (schema 'evidence.api.run/1'): HTTP requests, then CLI calls, each with what it must produce.</summary>
/// <param name="Schema">'evidence.api.run/1' when given.</param>
/// <param name="Requests">The HTTP requests, run first, in order.</param>
/// <param name="Cli">The CLI calls, run after the requests, in order.</param>
/// <param name="DeclaredWrites">Words (add, delete) the script declares as its writes, so a CLI argument reading as a write verb is allowed.</param>
/// <param name="PassEnv">Names of environment variables a CLI call may inherit, on top of the targets file's.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApiScript(
    string? Schema = null,
    IReadOnlyList<RequestStep>? Requests = null,
    IReadOnlyList<CliStep>? Cli = null,
    IReadOnlyList<string>? DeclaredWrites = null,
    IReadOnlyList<string>? PassEnv = null)
{
    /// <summary>The schema name scripts may state.</summary>
    public const string SchemaName = "evidence.api.run/1";

    /// <summary>The requests (empty when none).</summary>
    [JsonIgnore]
    public IReadOnlyList<RequestStep> RequestList => Requests ?? [];

    /// <summary>The CLI calls (empty when none).</summary>
    [JsonIgnore]
    public IReadOnlyList<CliStep> CliList => Cli ?? [];
}
