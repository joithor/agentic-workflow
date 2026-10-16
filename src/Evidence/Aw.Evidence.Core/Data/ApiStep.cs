namespace Aw.Evidence.Core.Data;

/// <summary>One executed request or CLI call.</summary>
/// <param name="Index">The step's 1-based position, requests first, then CLI calls.</param>
/// <param name="Action">Request or CLI call.</param>
/// <param name="Target">'GET /path', or the program and its words (no argument values that look like data).</param>
/// <param name="Status">How it ended.</param>
/// <param name="Code">The HTTP status or exit code; null when it never got one.</param>
/// <param name="Output">The body or stdout, scrubbed and capped; null when none.</param>
/// <param name="HeaderNames">Names of the headers sent (never values).</param>
public sealed record ApiStep(
    int Index,
    ApiAction Action,
    string Target,
    ApiStepStatus Status,
    int? Code,
    string? Output,
    IReadOnlyList<string> HeaderNames);
