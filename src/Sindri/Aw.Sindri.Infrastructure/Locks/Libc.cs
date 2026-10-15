using System.Reflection;
using System.Runtime.InteropServices;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>
/// mkdir fails with EEXIST when the directory is there, which makes it the mutex the shell tools use (config/lib/locks.sh);
/// Directory.CreateDirectory succeeds on an existing directory, so it can't take a lock.
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
            : throw new PlatformNotSupportedException("the heavy-job lock needs macOS or Linux (the C library's mkdir)");
    }
}
