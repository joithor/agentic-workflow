using System.Net;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A dev server 'sindri serve' started: its pid (recorded in control.json), its frontend and its database.</summary>
/// <param name="ProcessId">The 'temporal server start-dev' process.</param>
/// <param name="Frontend">The gRPC frontend, on loopback.</param>
/// <param name="DatabasePath">The SQLite file the server keeps its state in.</param>
internal sealed record RunningTemporalServer(int ProcessId, IPEndPoint Frontend, string DatabasePath);
