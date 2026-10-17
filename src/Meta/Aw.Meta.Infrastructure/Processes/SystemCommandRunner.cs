using System.ComponentModel;
using System.Diagnostics;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>
/// ICommandRunner over System.Diagnostics.Process. Both streams are read from the start, kept to their tail, and
/// appended to the request's log file as they arrive. On a timeout or a cancellation the whole process tree is killed.
/// </summary>
internal sealed class SystemCommandRunner : ICommandRunner
{
    private const int TailCharacters = 64 * 1024;
    private static readonly TimeSpan _drainGrace = TimeSpan.FromSeconds(2);

    public async ValueTask<Result<CommandResult>> Run(CommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Argv.Count == 0)
        {
            return MetaErrors.CommandNotStarted.With("empty command");
        }

        using var sandbox = request.Isolated ? Sandbox.Create() : null;
        using var process = new Process { StartInfo = StartInfo(request, sandbox) };
        try
        {
            if (!process.Start())
            {
                return MetaErrors.CommandNotStarted.With(request.Argv[0]);
            }
        }
        catch (Win32Exception exception)
        {
            return MetaErrors.CommandNotStarted.With($"{request.Argv[0]}: {exception.Message}");
        }

        var log = OpenLog(request.LogFile);
        try
        {
            var stdout = new TailBuffer(TailCharacters);
            var stderr = new TailBuffer(TailCharacters);
            process.StandardInput.Close();
            var reading = Task.WhenAll(Pump(process.StandardOutput, stdout, log), Pump(process.StandardError, stderr, log));
            var timedOut = await Wait(process, request.Timeout, cancellationToken).ConfigureAwait(false);
            _ = await Task.WhenAny(reading, Task.Delay(_drainGrace, CancellationToken.None)).ConfigureAwait(false);
            return new CommandResult(timedOut ? -1 : process.ExitCode, stdout.ToString(), stderr.ToString(), timedOut);
        }
        finally
        {
            if (log is not null)
            {
                await log.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static ProcessStartInfo StartInfo(CommandRequest request, Sandbox? sandbox)
    {
        var info = new ProcessStartInfo(ProgramPath.Resolve(request.Argv[0], request.Cwd))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in request.Argv.Skip(1))
        {
            info.ArgumentList.Add(argument);
        }

        if (request.Cwd is { Length: > 0 } cwd)
        {
            info.WorkingDirectory = cwd;
        }

        foreach (var name in request.UnsetEnv.Concat(sandbox is null ? [] : Sandbox.Unset))
        {
            _ = info.Environment.Remove(name);
        }

        foreach (var (name, value) in (sandbox?.Variables() ?? new Dictionary<string, string>(StringComparer.Ordinal)).Concat(request.Env))
        {
            info.Environment[name] = value;
        }

        return info;
    }

    private static StreamWriter? OpenLog(string? file)
    {
        if (file is null)
        {
            return null;
        }

        if (Path.GetDirectoryName(Path.GetFullPath(file)) is { } directory)
        {
            _ = Directory.CreateDirectory(directory);
        }

        return new StreamWriter(new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
    }

    // Returns true when the command was killed for running past its timeout; a cancellation kills it and propagates.
    private static async ValueTask<bool> Wait(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
    }

    private static async Task Pump(StreamReader reader, TailBuffer tail, StreamWriter? log)
    {
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            lock (tail)
            {
                tail.Append(buffer.AsSpan(0, read));
            }

            if (log is not null)
            {
                lock (log)
                {
                    log.Write(buffer, 0, read);
                }
            }
        }
    }
}
