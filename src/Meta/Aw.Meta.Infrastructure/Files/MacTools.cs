using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Runs one of the macOS tools (sysctl, vm_stat, ps) through the command runner and returns all of its output (ps lists far more than the runner's default tail), or AW-META-010 naming the tool and why.</summary>
internal static class MacTools
{
    /// <summary>
    /// How many characters of a tool's output are kept: 16 MiB, about thirty times what "ps -axww" prints for a busy machine
    /// (a few thousand processes with long command lines, well under 1 MiB). Past it the start of the output is dropped, never the end.
    /// </summary>
    internal const int OutputCharacterCap = 16 * 1024 * 1024;

    /// <summary>How many characters of a tool's stderr go into an error message.</summary>
    internal const int StderrCharacterCap = 300;

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    internal static async ValueTask<Result<string>> Output(ICommandRunner runner, IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        var ran = await runner.Run(new CommandRequest(argv, _timeout) { TailCharacters = OutputCharacterCap }, cancellationToken).ConfigureAwait(false);
        var tool = string.Join(' ', argv);
        if (ran.IsError)
        {
            return MetaErrors.HostUnreadable.With($"unavailable on macOS: {tool} could not be run ({ran.Error.Message})");
        }

        return ran.Value.TimedOut || ran.Value.ExitCode != 0
            ? MetaErrors.HostUnreadable.With($"unavailable on macOS: {tool} {(ran.Value.TimedOut ? "timed out" : string.Create(CultureInfo.InvariantCulture, $"exited {ran.Value.ExitCode}"))}: {Clip(ran.Value.Stderr.Trim())}")
            : ran.Value.Stdout;
    }

    /// <summary>Cuts a tool's stderr to <see cref="StderrCharacterCap"/> characters, marking the cut.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, or its start and a marker.</returns>
    internal static string Clip(string text) => text.Length <= StderrCharacterCap ? text : text[..StderrCharacterCap] + "... (truncated)";
}
