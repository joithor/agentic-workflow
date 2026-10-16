namespace Aw.Sindri.Infrastructure.Processes;

/// <summary>Runs a secret tool ('security', 'secret-tool', 'op') synchronously, with an argument list (never a shell string) and a timeout.</summary>
internal interface IToolRunner
{
    /// <summary>Runs the tool and waits for it, killing it past <paramref name="timeout"/>.</summary>
    /// <param name="fileName">The tool, looked up on PATH.</param>
    /// <param name="arguments">Its arguments, one per element.</param>
    /// <param name="timeout">How long it may run.</param>
    /// <returns>How it ended and what it printed on standard output.</returns>
    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout);
}
