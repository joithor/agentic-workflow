namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// External technology types (csharp.md Seams): database, cache, queue or bus, file system, processes, network, the
/// host environment, git, containers. Core, Features and the small shape's App.Commands see only ports. In Core,
/// BannedSymbols (RS0030) already bans the BCL I/O namespaces and types, System.Environment whole, the clock and random
/// ids; this list adds the packages it misses (Aw.Kernel.Data, SQLitePCL, Npgsql, cache and queue clients) and carries
/// the rule to Features and App.Commands, where System.Environment counts only through members that read the host.
/// Pure helpers such as System.Net.WebUtility, System.IO.TextWriter and Environment.NewLine are not technology.
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
        "System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo", "System.IO.File",
        "System.IO.FileInfo", "System.IO.Directory", "System.IO.DirectoryInfo", "System.IO.FileStream",
        "System.IO.FileSystemInfo", "System.IO.FileSystemWatcher", "System.IO.DriveInfo", "System.IO.RandomAccess",
        "System.IO.Compression.ZipFile", "System.IO.Compression.ZipFileExtensions", "System.Net.WebClient",
        "System.Net.WebRequest", "System.Net.HttpWebRequest", "System.Net.FtpWebRequest", "System.Net.HttpListener", "System.Net.Dns",
    ];

    // System.Environment's pure members; every other member (variables, folders, the current directory, the user and
    // machine, the command line, exit) reads or changes the host, so a new BCL member fails closed.
    private static readonly string[] _pureEnvironmentMembers =
    [
        "get_NewLine", "get_ProcessorCount", "get_TickCount", "get_TickCount64", "get_CurrentManagedThreadId",
        "get_Is64BitProcess", "get_Is64BitOperatingSystem", "get_Version", "get_HasShutdownStarted", "get_StackTrace",
    ];

    /// <summary>Whether a call to a member of a type reads or changes the host environment (any System.Environment member but the pure ones).</summary>
    /// <param name="fullName">The called type's full name.</param>
    /// <param name="member">The member, as ArchUnitNET names it (e.g. GetEnvironmentVariable(System.String)).</param>
    /// <returns>True for an environment read or write.</returns>
    public static bool IsEnvironmentAccess(string fullName, string member) =>
        fullName == "System.Environment" && !_pureEnvironmentMembers.Contains(member.Split('(')[0], StringComparer.Ordinal);

    /// <summary>What an edge's technology use is, for messages: the type, or the environment member called.</summary>
    /// <param name="edge">An edge that <see cref="Reaches"/> technology.</param>
    /// <returns>e.g. "uses a technology type" or "calls System.Environment.GetEnvironmentVariable".</returns>
    public static string Describe(Edge edge) =>
        Is(edge.Target.FullName)
            ? "uses a technology type"
            : $"calls {edge.Target.FullName}.{edge.TargetMembers.First(m => IsEnvironmentAccess(edge.Target.FullName, m)).Split('(')[0]}";

    /// <summary>Whether an edge reaches technology: a technology type, or an environment-reading member.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>True when the origin uses technology through this edge.</returns>
    public static bool Reaches(Edge edge) =>
        Is(edge.Target.FullName) || edge.TargetMembers.Any(m => IsEnvironmentAccess(edge.Target.FullName, m));

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
