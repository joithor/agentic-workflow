using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IProcessLocations over /proc (the working directory and every open file of each process the user may read), or lsof where there is
/// no /proc (macOS). A process that exits mid-scan, or belongs to someone else, is skipped; a scan that cannot run at all is an error.
/// </summary>
/// <param name="lsof">The lsof program (macOS: <see cref="MacToolPaths.Lsof"/>).</param>
internal sealed class ProcessScanner(string lsof) : IProcessLocations
{
    private const string Proc = "/proc";

    public async ValueTask<Result<IReadOnlyList<ProcessLocation>>> Locations(CancellationToken cancellationToken)
    {
        try
        {
            var found = Directory.Exists(Proc + "/self") ? ScanProc() : await Lsof(cancellationToken).ConfigureAwait(false);
            return Results.Success<IReadOnlyList<ProcessLocation>>(found);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return MetaErrors.HostUnreadable.With("the process scan failed: " + exception.Message);
        }
    }

    private static List<ProcessLocation> ScanProc()
    {
        var parents = new Dictionary<int, int>();
        var directories = new List<(int Pid, string Directory)>();
        foreach (var directory in Directory.EnumerateDirectories(Proc))
        {
            if (int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
            {
                directories.Add((pid, directory));
                if (Parent(directory) is { } parent)
                {
                    parents[pid] = parent;
                }
            }
        }

        var invoker = Chain(Environment.ProcessId, parents);
        var found = new List<ProcessLocation>();
        foreach (var (pid, directory) in directories)
        {
            if (Read(pid, directory, invoker.Contains(pid)) is { } location)
            {
                found.Add(location);
            }
        }

        return found;
    }

    private static HashSet<int> Chain(int start, Dictionary<int, int> parents)
    {
        var chain = new HashSet<int>();
        for (var pid = start; pid > 1 && chain.Add(pid) && parents.TryGetValue(pid, out var parent); pid = parent)
        {
            // each ancestor, up to init
        }

        return chain;
    }

    private static int? Parent(string directory)
    {
        try
        {
            return ProcFormat.Stat(File.ReadAllText(Path.Combine(directory, "stat")))?.Ppid;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    // A process the user can't read (another user's, or gone) holds nothing we could be asked about: skipped.
    private static ProcessLocation? Read(int pid, string directory, bool invoker)
    {
        try
        {
            var paths = new List<string>();
            AddTarget(paths, Path.Combine(directory, "cwd"));
            foreach (var descriptor in Directory.EnumerateFileSystemEntries(Path.Combine(directory, "fd")))
            {
                AddTarget(paths, descriptor);
            }

            var command = File.ReadAllText(Path.Combine(directory, "cmdline")).Replace('\0', ' ').Trim();
            return new ProcessLocation(pid, command.Length > 0 ? command : "[" + pid.ToString(CultureInfo.InvariantCulture) + "]", paths, invoker);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void AddTarget(List<string> paths, string link)
    {
        if (new FileInfo(link).LinkTarget is { Length: > 0 } target && target[0] == '/')
        {
            paths.Add(target);
        }
    }

    private async ValueTask<List<ProcessLocation>> Lsof(CancellationToken cancellationToken)
    {
        var info = new System.Diagnostics.ProcessStartInfo(lsof) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in (string[])["-w", "-n", "-F", "pcn"])
        {
            info.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("lsof could not be started");
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        _ = await errors.ConfigureAwait(false);
        var found = LsofOutput.Parse(output);
        return found.Count == 0
            ? throw new InvalidOperationException("lsof listed no process")
            : [.. found.Select(static p => p.Pid == Environment.ProcessId ? p with { IsInvoker = true } : p)];
    }
}
