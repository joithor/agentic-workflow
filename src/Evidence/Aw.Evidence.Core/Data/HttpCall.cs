namespace Aw.Evidence.Core.Data;

/// <summary>One HTTP request to send, exactly as the transport sends it.</summary>
/// <param name="Method">The method.</param>
/// <param name="Url">The absolute URL.</param>
/// <param name="Headers">Header name to value (resolved from the environment).</param>
/// <param name="Body">A JSON body (sent as application/json), or null for none.</param>
public sealed record HttpCall(HttpVerb Method, string Url, IReadOnlyDictionary<string, string> Headers, string? Body);
