using System.Net;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A dev server 'sindri serve' started or adopted: its pid (recorded in control.json), its frontend and its database.</summary>
/// <param name="ProcessId">The 'temporal server start-dev' process.</param>
/// <param name="Frontend">The gRPC frontend, on loopback.</param>
/// <param name="DatabasePath">The SQLite file the server keeps its state in.</param>
internal sealed record RunningTemporalServer(int ProcessId, IPEndPoint Frontend, string DatabasePath)
{
    /// <summary>The Web UI's port, on loopback.</summary>
    public int UiPort { get; init; }

    /// <summary>The 'temporal' executable the process runs.</summary>
    public string Executable { get; init; } = string.Empty;

    /// <summary>True when the server was already running and answered, so this start adopted it instead of starting one.</summary>
    public bool Adopted { get; init; }
}
