using System.Reflection;
using System.Runtime.InteropServices;

namespace Aw.Evidence.Infrastructure.Native;

/// <summary>
/// The C library calls the adapters need and .NET doesn't offer. mkdir fails with EEXIST when the directory is already
/// there, which makes it the box-wide mutex the shell tools use (config/lib/locks.sh); Directory.CreateDirectory
/// succeeds on an existing directory, so it can't take a lock. dup and dup2 point a file descriptor elsewhere for a
/// while (a child process inherits the process's descriptors, not Console's writers).
/// </summary>
internal static partial class Libc
{
    /// <summary>errno for "file exists" (the same value on macOS and Linux).</summary>
    internal const int AlreadyExists = 17;

    private const string Library = "libc";

    static Libc()
    {
        NativeLibrary.SetDllImportResolver(typeof(Libc).Assembly, Resolve);
    }

    /// <summary>Creates one directory atomically.</summary>
    /// <param name="path">The directory; its parent must exist.</param>
    /// <returns>0, or the errno when it failed (<see cref="AlreadyExists"/> when it is there).</returns>
    internal static int MakeDirectory(string path) =>
        Mkdir(path, Convert.ToUInt32("777", 8)) == 0 ? 0 : Marshal.GetLastPInvokeError();

    /// <summary>Duplicates a file descriptor.</summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>The new descriptor, or -1.</returns>
    [LibraryImport(Library, EntryPoint = "dup", SetLastError = true)]
    internal static partial int Duplicate(int descriptor);

    /// <summary>Makes <paramref name="target"/> refer to what <paramref name="source"/> refers to.</summary>
    /// <param name="source">The descriptor to copy.</param>
    /// <param name="target">The descriptor to replace.</param>
    /// <returns><paramref name="target"/>, or -1.</returns>
    [LibraryImport(Library, EntryPoint = "dup2", SetLastError = true)]
    internal static partial int DuplicateOnto(int source, int target);

    /// <summary>Closes a file descriptor.</summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>0, or -1.</returns>
    [LibraryImport(Library, EntryPoint = "close", SetLastError = true)]
    internal static partial int Close(int descriptor);

    [LibraryImport(Library, EntryPoint = "mkdir", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Mkdir(string path, uint mode);

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(name, Library, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        return OperatingSystem.IsMacOS() ? NativeLibrary.Load("/usr/lib/libSystem.dylib")
            : OperatingSystem.IsLinux() ? NativeLibrary.Load("libc.so.6")
            : throw new PlatformNotSupportedException("ui-evidence needs macOS or Linux (the C library's mkdir and dup2)");
    }
}
