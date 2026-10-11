using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Processes;

/// <summary>Runs a command to completion (argument list, never a shell), with optional standard input and a time limit.</summary>
internal static class ProcessRunner
{
    /// <summary>Runs <paramref name="command"/>.</summary>
    /// <param name="command">The program, then its arguments.</param>
    /// <param name="standardInput">Text to write to its standard input, or null for none.</param>
    /// <param name="timeout">How long it may run before it is killed.</param>
    /// <param name="failure">The error to report when it can't start or runs out of time.</param>
    /// <param name="cancellationToken">Cancels the run (the process is killed).</param>
    /// <returns>The outcome, or <paramref name="failure"/> saying what went wrong.</returns>
    internal static async ValueTask<Result<ProcessOutcome>> RunAsync(
        IReadOnlyList<string> command, string? standardInput, TimeSpan timeout, ErrorDefinition failure, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = StartInfo(command, standardInput is not null) };
        try
        {
            _ = process.Start();
        }
        catch (Win32Exception ex)
        {
            return failure.With($"{command[0]} could not start: {ex.Message}");
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            return await CollectAsync(process, standardInput, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return failure.With(string.Create(CultureInfo.InvariantCulture, $"{command[0]} did not finish within {timeout.TotalSeconds:0} s"));
        }
    }

    private static async Task<ProcessOutcome> CollectAsync(Process process, string? standardInput, CancellationToken cancellationToken)
    {
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    private static ProcessStartInfo StartInfo(IReadOnlyList<string> command, bool redirectInput)
    {
        var start = new ProcessStartInfo(command[0])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectInput,
            UseShellExecute = false,
        };
        foreach (var argument in command.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }
}
