using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// Process ancestry on macOS and Linux, one parent at a time and without starting a process per level. The parent pid
/// comes from proc_pidinfo(PROC_PIDTBSDINFO) on macOS and /proc/&lt;pid&gt;/stat on Linux. The name is the basename of
/// argv[0] (sysctl KERN_PROCARGS2, /proc/&lt;pid&gt;/cmdline), as ps shows it, because the kernel's own name is the
/// executable's file name: Claude Code runs as '~/.local/share/claude/versions/2.1.296', invoked as 'claude'. The
/// kernel's name stands in when argv can't be read. A level the OS won't show (a sandbox, another user's process, a
/// pid that exited) ends the walk.
/// </summary>
internal sealed partial class PosixProcessAncestry : IProcessAncestry
{
    private const string LibSystem = "/usr/lib/libSystem.dylib";
    private const int BsdInfoFlavor = 3;     // PROC_PIDTBSDINFO
    private const int BsdInfoSize = 136;     // sizeof(struct proc_bsdinfo)
    private const int ParentOffset = 16;     // pbi_ppid
    private const int CommandOffset = 48;    // pbi_comm[16]
    private const int NameOffset = 64;       // pbi_name[32]
    private const int KernelControl = 1;     // CTL_KERN
    private const int ArgumentMax = 8;       // KERN_ARGMAX
    private const int ProcessArguments = 49; // KERN_PROCARGS2

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
        var kernelName = name.Length > 0 ? name : Text(info.AsSpan(CommandOffset, 16));
        return ((int)BinaryPrimitives.ReadUInt32LittleEndian(info.AsSpan(ParentOffset)), BaseName(ArgumentZeroMac(pid)) ?? kernelName);
    }

    /// <summary>argv[0] from KERN_PROCARGS2: argc (int32), the exec path, NUL padding, then argv[0].</summary>
    private static string? ArgumentZeroMac(int pid)
    {
        int[] argMax = [KernelControl, ArgumentMax];
        var size = 0;
        var sizeLength = (nuint)sizeof(int);
        if (Sysctl(ref argMax[0], 2, ref Unsafe(ref size), ref sizeLength, IntPtr.Zero, 0) != 0 || size <= sizeof(int))
        {
            return null;
        }

        int[] request = [KernelControl, ProcessArguments, pid];
        var buffer = new byte[size];
        var length = (nuint)size;
        if (Sysctl(ref request[0], 3, ref buffer[0], ref length, IntPtr.Zero, 0) != 0 || length < sizeof(int))
        {
            return null;   // no argc, so no argument zero: the caller falls back to the kernel's name
        }

        var rest = buffer.AsSpan(sizeof(int), (int)length - sizeof(int));
        var pathEnd = rest.IndexOf((byte)0);
        if (pathEnd < 0)
        {
            return null;
        }

        rest = rest[pathEnd..].TrimStart((byte)0);
        var argument = Text(rest);
        return argument.Length > 0 ? argument : null;
    }

    private static (int Parent, string Name)? ReadLinux(int pid)
    {
        string stat;
        string? arguments;
        try
        {
            stat = File.ReadAllText(string.Create(CultureInfo.InvariantCulture, $"/proc/{pid}/stat"));
            arguments = File.ReadAllText(string.Create(CultureInfo.InvariantCulture, $"/proc/{pid}/cmdline"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        // "pid (comm) state ppid …": comm may hold spaces and parentheses, so it ends at the last ')'.
        var open = stat.IndexOf('(', StringComparison.Ordinal);
        var close = stat.LastIndexOf(')');
        var fields = close > open && open >= 0 ? stat[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
        var argumentZero = arguments.Split('\0')[0];
        return fields.Length > 1 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent)
            ? (parent, BaseName(argumentZero.Length > 0 ? argumentZero : null) ?? stat[(open + 1)..close])
            : null;
    }

    private static string? BaseName(string? path) => path is null ? null : path[(path.LastIndexOf('/') + 1)..] is { Length: > 0 } name ? name : null;

    private static string Text(ReadOnlySpan<byte> field)
    {
        var end = field.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? field : field[..end]);
    }

    private static ref byte Unsafe(ref int value) => ref MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1))[0];

    [LibraryImport(LibSystem, EntryPoint = "proc_pidinfo")]
    private static partial int ProcPidInfo(int pid, int flavor, ulong argument, ref byte buffer, int bufferSize);

    [LibraryImport(LibSystem, EntryPoint = "sysctl")]
    private static partial int Sysctl(ref int name, uint nameLength, ref byte value, ref nuint valueLength, IntPtr newValue, nuint newLength);
}
