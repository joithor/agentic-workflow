namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// External technology types (csharp.md Seams): database, cache, queue or bus, file system, processes, HTTP, git,
/// containers. Core, Features and Commands see only ports; BannedSymbols covers Core's BCL I/O, and this list adds
/// the packages it misses (Aw.Kernel.Data, SQLitePCL, Npgsql, cache and queue clients).
/// </summary>
public static class Technology
{
    private static readonly string[] _namespaces =
    [
        "System.Net", "System.Data", "Microsoft.Data", "Aw.Kernel.Data", "SQLitePCL", "Npgsql", "StackExchange.Redis",
        "Microsoft.Extensions.Caching", "RabbitMQ", "Confluent.Kafka", "NATS", "Azure", "Amazon", "Docker.DotNet",
        "LibGit2Sharp", "System.IO.Pipes", "System.IO.MemoryMappedFiles", "System.IO.Ports", "Microsoft.Win32",
    ];

    private static readonly string[] _types =
    [
        "System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo", "System.IO.File", "System.IO.FileInfo",
        "System.IO.Directory", "System.IO.DirectoryInfo", "System.IO.FileStream", "System.IO.FileSystemInfo",
        "System.IO.FileSystemWatcher", "System.IO.DriveInfo", "System.IO.RandomAccess",
    ];

    /// <summary>Whether a type (by full name) is an external technology type.</summary>
    /// <param name="fullName">The type's full name.</param>
    /// <returns>True for a technology type.</returns>
    public static bool Is(string fullName)
    {
        var name = fullName.Split('+', '/', '`')[0];
        return _types.Contains(name, StringComparer.Ordinal)
            || _namespaces.Any(n => name.StartsWith(n + ".", StringComparison.Ordinal));
    }
}
