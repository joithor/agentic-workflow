using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Forge.Infrastructure.Git;

/// <summary>Runs one program with an exact argument list and an environment built from scratch plus what the caller adds.</summary>
internal static class ProcessRunner
{
    /// <summary>Environment variables kept from the parent: enough to find programs and a home, nothing git reads.</summary>
    private static readonly string[] Inherited = ["PATH", "HOME", "USERPROFILE", "SYSTEMROOT", "TMPDIR", "TEMP", "TMP", "LANG", "LC_ALL"];

    internal static async Task<ProcessResult?> Run(
        string file,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        string? workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (workingDirectory is not null)
        {
            info.WorkingDirectory = workingDirectory;
        }

        info.Environment.Clear();
        foreach (var name in Inherited)
        {
            if (Environment.GetEnvironmentVariable(name) is { } value)
            {
                info.Environment[name] = value;
            }
        }

        foreach (var (name, value) in environment)
        {
            info.Environment[name] = value;
        }

        using var process = new Process { StartInfo = info };
        try
        {
            if (!process.Start())
            {
                return null;
            }
        }
        catch (Win32Exception)
        {
            return null;
        }

        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ProcessResult(-1, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
        }

        return new ProcessResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }
}
