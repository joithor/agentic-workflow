using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The probes on the time-skipping test server (temporal-test-server, binaries.lock).</summary>
/// <param name="server">The server.</param>
[Lane(TestLane.Integration)]
public sealed class TimeSkippingProbeTests(TimeSkippingServer server) : TemporalProbes(server), IClassFixture<TimeSkippingServer>;
