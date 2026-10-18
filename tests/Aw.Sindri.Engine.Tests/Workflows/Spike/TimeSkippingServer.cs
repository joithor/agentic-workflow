namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The time-skipping test server, once per test class.</summary>
public sealed class TimeSkippingServer : TemporalServerFixture
{
    /// <inheritdoc/>
    public override ServerKind Kind => ServerKind.TimeSkipping;
}
