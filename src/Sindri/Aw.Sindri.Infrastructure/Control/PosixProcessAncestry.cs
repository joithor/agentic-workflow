using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// Process ancestry on macOS and Linux, one parent at a time and without starting a process per level: macOS reads
/// proc_pidinfo(PROC_PIDTBSDINFO) from libproc, Linux reads /proc/&lt;pid&gt;/stat. A level the OS won't show (a sandbox,
/// another user's process, a pid that exited) ends the walk.
/// </summary>
internal sealed partial class PosixProcessAncestry : IProcessAncestry
{
    private const int BsdInfoFlavor = 3;     // PROC_PIDTBSDINFO
    private const int BsdInfoSize = 136;     // sizeof(struct proc_bsdinfo)
    private const int ParentOffset = 16;     // pbi_ppid
    private const int CommandOffset = 48;    // pbi_comm[16]
    private const int NameOffset = 64;       // pbi_name[32]

    public IReadOnlyList<ProcessAncestor> Ancestors(int pid, int max)
    {
        var found = new List<ProcessAncestor>();
        var current = pid;
        while (found.Count < max && current != 1 && Read(current) is { Parent: > 0 } self && Read(self.Parent) is { } parent)
        {
            found.Add(new ProcessAncestor(self.Parent, parent.Name));
            current = self.Parent;
        }

        return found;
    }

    private static (int Parent, string Name)? Read(int pid) =>
        OperatingSystem.IsMacOS() ? ReadMac(pid) : OperatingSystem.IsLinux() ? ReadLinux(pid) : null;

    private static (int Parent, string Name)? ReadMac(int pid)
    {
        if (pid == 1)
        {
            return (0, "launchd");   // pid 1 is always launchd, and proc_pidinfo refuses root's processes to other users
        }

        var info = new byte[BsdInfoSize];
        if (ProcPidInfo(pid, BsdInfoFlavor, 0, ref info[0], BsdInfoSize) != BsdInfoSize)
        {
            return null;
        }

        var name = Text(info.AsSpan(NameOffset, 32));
        return ((int)BinaryPrimitives.ReadUInt32LittleEndian(info.AsSpan(ParentOffset)), name.Length > 0 ? name : Text(info.AsSpan(CommandOffset, 16)));
    }

    private static (int Parent, string Name)? ReadLinux(int pid)
    {
        string stat;
        try
        {
            stat = File.ReadAllText(string.Create(CultureInfo.InvariantCulture, $"/proc/{pid}/stat"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        // "pid (comm) state ppid …": comm may hold spaces and parentheses, so it ends at the last ')'.
        var open = stat.IndexOf('(', StringComparison.Ordinal);
        var close = stat.LastIndexOf(')');
        var fields = close > open && open >= 0 ? stat[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
        return fields.Length > 1 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent)
            ? (parent, stat[(open + 1)..close])
            : null;
    }

    private static string Text(ReadOnlySpan<byte> field)
    {
        var end = field.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? field : field[..end]);
    }

    [LibraryImport("libproc", EntryPoint = "proc_pidinfo")]
    private static partial int ProcPidInfo(int pid, int flavor, ulong argument, ref byte buffer, int bufferSize);
}
