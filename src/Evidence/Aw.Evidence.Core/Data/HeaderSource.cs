using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>Where a request header's value comes from: the environment variable it names, never a literal in the script.</summary>
/// <param name="Env">The variable's name (UPPER_SNAKE).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonConverter(typeof(HeaderSourceConverter))]
public sealed record HeaderSource(string Env);
