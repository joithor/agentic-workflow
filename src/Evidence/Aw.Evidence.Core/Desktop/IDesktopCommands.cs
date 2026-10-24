using Aw.Kernel;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Runs the repo's own fixture and capture commands (arguments, never a shell).</summary>
public interface IDesktopCommands
{
    /// <summary>Runs <paramref name="command"/> to completion.</summary>
    /// <param name="command">The program, its working directory, its extra environment and its time limit.</param>
    /// <param name="cancellationToken">Cancels the run (the process is killed).</param>
    /// <returns>How it ended, or AW-EVIDENCE-015 when it cannot start or runs out of time.</returns>
    public ValueTask<Result<CommandResult>> RunAsync(CommandSpec command, CancellationToken cancellationToken);
}

/// <summary>A program to run for a desktop run.</summary>
/// <param name="Argv">The program, then its arguments; a relative program is the working directory's.</param>
/// <param name="WorkingDirectory">Where it runs (the repo).</param>
/// <param name="Environment">Variables added to the inherited environment.</param>
/// <param name="Timeout">How long it may run.</param>
public sealed record CommandSpec(IReadOnlyList<string> Argv, string WorkingDirectory, IReadOnlyDictionary<string, string> Environment, TimeSpan Timeout);

/// <summary>How a command ended.</summary>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="StandardOutput">What it wrote to standard output.</param>
/// <param name="StandardError">What it wrote to standard error.</param>
public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
