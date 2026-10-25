using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The probes on the time-skipping test server (temporal-test-server, binaries.lock).</summary>
/// <param name="server">The server.</param>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]   // wall clock (it asserts a 4 h timer takes under a second of wall clock)
public sealed class TimeSkippingProbeTests(TimeSkippingServer server) : TemporalProbes(server), IClassFixture<TimeSkippingServer>;
