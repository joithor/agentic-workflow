using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;

namespace Aw.Judge.Infrastructure.Processes;

/// <summary>
/// IProcessRunner over System.Diagnostics.Process. Both output streams are read concurrently from the start and the
/// input is written on its own task, so a program that fills a pipe never blocks; on a timeout or a cancellation the
/// whole process tree is killed, so a child that inherited the pipes can't keep the run open.
/// </summary>
/// <param name="environment">Reads PATH, to find a program given by bare name.</param>
internal sealed class SystemProcessRunner(Func<string, string?> environment) : IProcessRunner
{
    // ENOENT on Unix and ERROR_FILE_NOT_FOUND on Windows: the program vanished between the lookup and the start.
    private const int NoSuchFile = 2;

    // After a kill, how long to wait for the pipes to drain before giving up on the rest of the output.
    private static readonly TimeSpan _drainGrace = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async ValueTask<ProcessResult> Run(ProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (Resolve(request.FileName) is not { } file)
        {
            return ProcessResult.NotStarted();
        }

        using var process = new Process { StartInfo = StartInfo(file, request) };
        if (Start(process) is { } refused)
        {
            return refused;
        }

        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var stdin = WriteInput(process.StandardInput, request.StandardInput);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.Timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr, stdin).WaitAsync(deadline.Token).ConfigureAwait(false);
            return ProcessResult.Exited(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            KillTree(process);
            cancellationToken.ThrowIfCancellationRequested();
            return ProcessResult.TimedOutAfter(await Drained(stdout).ConfigureAwait(false), await Drained(stderr).ConfigureAwait(false));
        }
    }

    // Null once it runs; otherwise why it didn't.
    private static ProcessResult? Start(Process process)
    {
        try
        {
            return process.Start() ? null : ProcessResult.NotStarted();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == NoSuchFile)
        {
            return ProcessResult.NotStarted();
        }
        catch (Win32Exception exception)
        {
            // It is installed, so this is a real failure (E2BIG, ENOEXEC, EACCES...), not a missing program.
            return ProcessResult.FailedToStart(exception.Message);
        }
    }

    private static ProcessStartInfo StartInfo(string file, ProcessRequest request)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = request.WorkingDirectory ?? "",
        };
        foreach (var argument in request.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in request.Environment)
        {
            info.Environment[name] = value;
        }

        return info;
    }

    // A bare name is looked up on PATH as the environment gives it; anything with a directory part is used as is.
    private string? Resolve(string fileName) =>
        fileName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) || fileName.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            ? SearchPath.IsExecutable(fileName) ? fileName : null
            : SearchPath.Find(environment("PATH"), fileName);

    // Writes the input and closes the stream; a program that exits without reading it (a broken pipe) is not an error.
    private static async Task WriteInput(StreamWriter input, string? text)
    {
        try
        {
            if (text is not null)
            {
                await input.WriteAsync(text).ConfigureAwait(false);
                await input.FlushAsync().ConfigureAwait(false);
            }

            input.Close();
        }
        catch (IOException)
        {
            // The program closed its end first; what it read is all it wanted.
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It exited between the timeout and the kill.
        }
        catch (Win32Exception)
        {
            // A process in the tree couldn't be signalled (it is already gone).
        }
    }

    private static async Task<string> Drained(Task<string> stream)
    {
        try
        {
            return await stream.WaitAsync(_drainGrace).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return "";
        }
    }
}
