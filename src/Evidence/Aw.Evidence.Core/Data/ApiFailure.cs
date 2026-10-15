using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>One assertion that did not hold.</summary>
/// <param name="Step">The step's 1-based index.</param>
/// <param name="Location">The JSON Pointer asserted on; null for a status, exit code or golden check.</param>
/// <param name="Expected">What was expected (text, capped).</param>
/// <param name="Actual">What was found (text, capped; 'missing' for an absent pointer).</param>
public sealed record ApiFailure(int Step, [property: JsonPropertyName("pointer")] string? Location, string Expected, string Actual);
