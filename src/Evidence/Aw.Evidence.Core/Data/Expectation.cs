using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>What a request or a CLI call must produce: the status (request) or exit code (call), pointer assertions over the body or stdout, and a golden file.</summary>
/// <param name="Status">The final response's HTTP status (requests only).</param>
/// <param name="ExitCode">The process's exit code (CLI calls only).</param>
/// <param name="Pointers">JSON Pointer assertions over the response body, or over stdout read as JSON.</param>
/// <param name="Golden">A file the body or stdout must equal; a relative path resolves against the script's directory.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Expectation(
    int? Status = null,
    int? ExitCode = null,
    IReadOnlyList<PointerCheck>? Pointers = null,
    string? Golden = null);
