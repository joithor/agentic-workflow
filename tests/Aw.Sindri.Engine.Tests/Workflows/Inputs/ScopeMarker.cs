namespace Aw.Sindri.Tests.Workflows.Inputs;

/// <summary>A per-scope service: every DI scope gets its own id.</summary>
internal sealed class ScopeMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}
