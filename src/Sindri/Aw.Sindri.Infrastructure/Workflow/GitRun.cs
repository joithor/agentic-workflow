namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>How a git call ended.</summary>
/// <param name="Exit">The exit code, or -1 when git didn't run.</param>
/// <param name="Output">Standard output, trimmed.</param>
/// <param name="Error">Standard error.</param>
internal sealed record GitRun(int Exit, string Output, string Error);
