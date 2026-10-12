namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The temporal CLI dev server, once per test class.</summary>
public sealed class LocalServer : TemporalServerFixture
{
    /// <inheritdoc/>
    public override ServerKind Kind => ServerKind.Local;
}
