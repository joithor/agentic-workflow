using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>One HTTP request to the run's host.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">A path on the host: one leading '/', never another host.</param>
/// <param name="Expect">What the final response must hold; its status is required.</param>
/// <param name="Headers">Header name to the environment variable that holds its value.</param>
/// <param name="Body">A JSON body, sent as application/json (not on GET or HEAD); null or absent sends none.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestStep(
    HttpVerb Method,
    string Path,
    Expectation Expect,
    IReadOnlyDictionary<string, HeaderSource>? Headers = null,
    JsonElement? Body = null);
