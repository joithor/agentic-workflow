using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The probes on the dev server (temporal CLI, binaries.lock).</summary>
/// <param name="server">The server.</param>
[Lane(TestLane.Integration)]
public sealed class LocalProbeTests(LocalServer server) : TemporalProbes(server), IClassFixture<LocalServer>;
