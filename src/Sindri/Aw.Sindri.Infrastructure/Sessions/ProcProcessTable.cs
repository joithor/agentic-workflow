using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>
/// The process table over /proc (collector spec §4.1 steps 1, 2, 5, 6; CI-3). The root is injectable (AW_SESSIONS_PROC_ROOT, a test override). A process is
/// the current user's when its status file's 'Uid:' line says so: compared with an own uid passed in, never the owner of the directory.
/// </summary>
/// <param name="root">The /proc directory (or a fixture tree shaped like it).</param>
/// <param name="ownUid">The uid that counts as ours.</param>
internal sealed class ProcProcessTable(string root, int ownUid) : IProcessTable
{
    private const int FdLimit = 64;

    public Result<IReadOnlyList<ProcessEntry>> Read(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return UnavailableReasons.ToError(UnavailableReason.UnsupportedPlatform);   // no /proc here (macOS, Windows)
        }

        IEnumerable<string> directories;
        try
        {
            directories = [.. Directory.EnumerateDirectories(root)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Unreadable();
        }

        var table = new List<ProcessEntry>();
        foreach (var directory in directories)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SindriErrors.ApiTimeout.With("the process table read was cancelled");
            }

            if (int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && Entry(pid, directory) is { } entry)
            {
                table.Add(entry);
            }
        }

        return table.Count == 0 ? Unreadable() : Results.Success<IReadOnlyList<ProcessEntry>>([.. table.OrderBy(static p => p.Pid)]);
    }

    public IReadOnlyDictionary<string, string> Environment(int pid) =>
        EnvironmentAllowlist.Filter(ProcFiles.Environ(Path.Combine(Dir(pid), "environ")));

    public string? Cwd(int pid) => ProcFiles.Link(Path.Combine(Dir(pid), "cwd"));

    public IReadOnlyList<string> OpenFiles(int pid)
    {
        try
        {
            return [.. Directory.EnumerateFileSystemEntries(Path.Combine(Dir(pid), "fd")).Take(FdLimit).Select(ProcFiles.Link).OfType<string>()];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static Error Unreadable() => UnavailableReasons.ToError(UnavailableReason.ProcUnreadable);

    private string Dir(int pid) => Path.Combine(root, pid.ToString(CultureInfo.InvariantCulture));

    private ProcessEntry? Entry(int pid, string directory)
    {
        if (ProcFiles.RealUid(Path.Combine(directory, "status")) != ownUid || ProcFiles.Stat(Path.Combine(directory, "stat")) is not var (comm, fields) || fields.Length < 20)
        {
            return null;
        }

        if (string.Equals(fields[0], "Z", StringComparison.Ordinal) || string.Equals(fields[0], "X", StringComparison.Ordinal) || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent)
            || !int.TryParse(fields[4], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var tty)
            || !long.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
        {
            return null;
        }

        return new ProcessEntry(pid, parent, comm, tty, ticks)
        {
            Argv0 = ProcFiles.ArgvZero(Path.Combine(directory, "cmdline")),
            ExePath = ProcFiles.Link(Path.Combine(directory, "exe")),
        };
    }
}
