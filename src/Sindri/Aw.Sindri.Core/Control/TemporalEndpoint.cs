namespace Aw.Sindri.Core.Control;

/// <summary>
/// The Temporal dev server 'sindri serve' keeps beside itself (engine spec rev 8 §0 "Operations"), as control.json
/// records it: the 'temporal server start-dev' process and its loopback ports.
/// </summary>
/// <param name="Pid">The dev server's process id.</param>
/// <param name="Port">Its gRPC frontend on 127.0.0.1.</param>
/// <param name="UiPort">Its Web UI on 127.0.0.1 (a debugging view only).</param>
public sealed record TemporalEndpoint(int Pid, int Port, int UiPort);
