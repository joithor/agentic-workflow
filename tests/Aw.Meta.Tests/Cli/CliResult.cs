namespace Aw.Meta.Tests.Cli;

/// <summary>The result of one aw-meta call.</summary>
/// <param name="Exit">The exit code.</param>
/// <param name="Out">Standard output.</param>
/// <param name="Err">Standard error.</param>
internal sealed record CliResult(int Exit, string Out, string Err);
