using System.Reflection;
using System.Runtime.InteropServices;
using SQLitePCL;

namespace Aw.Kernel.Data;

/// <summary>
/// Which native SQLite every connection uses, and whether it is new enough (spec §4b.2, decision R8). By default the
/// OS libsqlite3 (SQLitePCLRaw's sqlite3 provider), so an AOT tool is one file. A tool whose supported hosts ship too
/// old a SQLite calls <see cref="Initialize"/> with the bundled e_sqlite3 provider before its first connection, and
/// lists that library in binaries.lock. Every connection is refused (AW-DATA-002) when the library is older than
/// <see cref="MinimumVersion"/> or lacks the JSON functions.
/// </summary>
public static class SqliteRuntime
{
    /// <summary>The oldest SQLite accepted: 3.38.0, the first with the JSON functions built in by default.</summary>
    public const string MinimumVersion = "3.38.0";

    private const int MinimumVersionNumber = 3_038_000;

    private static readonly Lock _gate = new();

    private static bool _initialized;

    private static Result<string>? _check;

    /// <summary>The version of the SQLite library in use, for example '3.51.0'.</summary>
    public static string Version
    {
        get
        {
            EnsureInitialized();
            return raw.sqlite3_libversion().utf8_to_string();
        }
    }

    /// <summary>
    /// Chooses the native provider. Call it once, before the first connection, only to use something other than the
    /// OS library (for example 'new SQLite3Provider_e_sqlite3()' from SQLitePCLRaw.bundle_e_sqlite3).
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <exception cref="InvalidOperationException">A provider was already chosen (explicitly or by a connection).</exception>
    public static void Initialize(ISQLite3Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_gate)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("The SQLite provider is already chosen; call SqliteRuntime.Initialize before the first connection.");
            }

            raw.SetProvider(provider);
            _initialized = true;
        }
    }

    /// <summary>Checks the library once: its version and the JSON functions. Doctor commands report this.</summary>
    /// <returns>The version, or AW-DATA-002 naming what is missing.</returns>
    public static Result<string> Check()
    {
        EnsureInitialized();
        lock (_gate)
        {
            return _check ??= Evaluate(raw.sqlite3_libversion_number(), raw.sqlite3_libversion().utf8_to_string(), HasJson());
        }
    }

    internal static Result<string> Evaluate(int number, string version, bool json)
    {
        if (number < MinimumVersionNumber)
        {
            return DataErrors.Unavailable.With(
                $"SQLite {version} is older than {MinimumVersion}. Update the OS SQLite, or build this tool with the bundled e_sqlite3 (docs/csharp/kernel-data.md).");
        }

        return json
            ? version
            : DataErrors.Unavailable.With($"SQLite {version} was built without the JSON functions. Build this tool with the bundled e_sqlite3.");
    }

    internal static void EnsureInitialized()
    {
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                // Distributions ship libsqlite3.so.0; the unversioned libsqlite3.so only comes with the -dev package.
                NativeLibrary.SetDllImportResolver(typeof(SQLite3Provider_sqlite3).Assembly, ResolveLinux);
            }

            raw.SetProvider(new SQLite3Provider_sqlite3());
            _initialized = true;
        }
    }

    private static IntPtr ResolveLinux(string name, Assembly assembly, DllImportSearchPath? path) =>
        string.Equals(name, "sqlite3", StringComparison.Ordinal) && NativeLibrary.TryLoad("libsqlite3.so.0", assembly, path, out var handle)
            ? handle
            : IntPtr.Zero;

    private static bool HasJson()
    {
        var opened = raw.sqlite3_open_v2(":memory:", out var db, raw.SQLITE_OPEN_READWRITE | raw.SQLITE_OPEN_CREATE, vfs: null);
        using (db)
        {
            if (opened != raw.SQLITE_OK)
            {
                return false;
            }

            var prepared = raw.sqlite3_prepare_v2(db, "SELECT json_valid('{}')", out var statement);
            using (statement)
            {
                return prepared == raw.SQLITE_OK
                    && raw.sqlite3_step(statement) == raw.SQLITE_ROW
                    && raw.sqlite3_column_int(statement, 0) == 1;
            }
        }
    }
}
