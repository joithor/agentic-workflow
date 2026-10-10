namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// External technology types (csharp.md Seams): database, cache, queue or bus, file system, processes, network, the
/// environment, git, containers. Core, Features and the small shape's App.Commands see only ports; BannedSymbols covers
/// Core's BCL I/O, and this list adds what it misses (Aw.Kernel.Data, SQLitePCL, Npgsql, cache and queue clients, the
/// environment). Pure helpers such as System.Net.WebUtility and System.IO.TextWriter are not technology.
/// </summary>
public static class Technology
{
    private static readonly string[] _namespaces =
    [
        "System.Net.Http", "System.Net.Sockets", "System.Net.NetworkInformation", "System.Net.Security", "System.Net.Mail",
        "System.Net.WebSockets", "System.Net.Quic", "System.Data", "Microsoft.Data", "Aw.Kernel.Data", "SQLitePCL", "Npgsql",
        "Microsoft.EntityFrameworkCore", "Dapper", "MongoDB", "StackExchange.Redis", "Microsoft.Extensions.Caching", "RabbitMQ",
        "Confluent.Kafka", "NATS", "Grpc", "Azure", "Amazon", "Docker.DotNet", "LibGit2Sharp", "System.IO.Pipes",
        "System.IO.MemoryMappedFiles", "System.IO.Ports", "Microsoft.Win32",
    ];

    private static readonly string[] _types =
    [
        "System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo", "System.Environment", "System.IO.File",
        "System.IO.FileInfo", "System.IO.Directory", "System.IO.DirectoryInfo", "System.IO.FileStream",
        "System.IO.FileSystemInfo", "System.IO.FileSystemWatcher", "System.IO.DriveInfo", "System.IO.RandomAccess",
        "System.IO.Compression.ZipFile", "System.IO.Compression.ZipFileExtensions", "System.Net.WebClient",
        "System.Net.WebRequest", "System.Net.HttpWebRequest", "System.Net.FtpWebRequest", "System.Net.HttpListener", "System.Net.Dns",
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
