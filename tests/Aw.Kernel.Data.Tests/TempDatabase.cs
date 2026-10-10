namespace Aw.Kernel.Data.Tests;

/// <summary>A database file in its own temp directory, deleted with its WAL and SHM files afterwards.</summary>
public sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        Directory = System.IO.Directory.CreateTempSubdirectory("aw-kernel-data-").FullName;
        Path = System.IO.Path.Combine(Directory, "test.db");
    }

    public string Directory { get; }

    public string Path { get; }

    public SqliteOptions Options(TimeSpan? busyTimeout = null) =>
        new(Path) { BusyTimeout = busyTimeout ?? TimeSpan.FromSeconds(5) };

    public SqliteConnectionFactory Factory(TimeSpan? busyTimeout = null) => new(Options(busyTimeout));

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
}
