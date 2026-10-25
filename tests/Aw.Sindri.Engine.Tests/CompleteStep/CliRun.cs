namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>One in-process run of a 'sindri' verb: its exit code and what it wrote.</summary>
/// <param name="Exit">The exit code.</param>
/// <param name="Stdout">Standard output.</param>
/// <param name="Stderr">Standard error.</param>
internal sealed record CliRun(int Exit, string Stdout, string Stderr)
{
    /// <summary>Standard output's lines, without the last newline.</summary>
    public string[] Lines => Stdout.TrimEnd('\n').Split('\n');
}
