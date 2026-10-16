namespace Aw.Sindri.Infrastructure.Processes;

/// <summary>Runs an external tool ('security', 'secret-tool', 'op', 'docker') synchronously, with an argument list (never a shell string) and a timeout.</summary>
internal interface IToolRunner
{
    /// <summary>Runs the tool and waits for it, killing it past <paramref name="timeout"/> or when <paramref name="cancellationToken"/> is cancelled.</summary>
    /// <param name="fileName">The tool, looked up on PATH.</param>
    /// <param name="arguments">Its arguments, one per element.</param>
    /// <param name="timeout">How long it may run.</param>
    /// <param name="standardErrorLine">Called once per standard error line while the tool runs (a pull's progress); null drops standard error. Standard output is still captured whole.</param>
    /// <param name="cancellationToken">Cancels the run: the tool and its children are killed.</param>
    /// <returns>How it ended and what it printed on standard output.</returns>
    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, Action<string>? standardErrorLine = null, CancellationToken cancellationToken = default);
}
