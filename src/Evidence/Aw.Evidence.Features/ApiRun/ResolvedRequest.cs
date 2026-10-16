using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>A request with the header values read from the environment and the golden file's text, both resolved before any lock.</summary>
/// <param name="Step">The request.</param>
/// <param name="Headers">Header name to value.</param>
/// <param name="GoldenText">The golden file's text, when the expectation names one.</param>
internal sealed record ResolvedRequest(RequestStep Step, IReadOnlyDictionary<string, string> Headers, string? GoldenText);
