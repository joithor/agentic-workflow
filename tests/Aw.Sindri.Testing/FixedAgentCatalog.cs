using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IAgentCatalog"/> over the entries a test names.</summary>
/// <param name="entries">The entries.</param>
public sealed class FixedAgentCatalog(params AgentEntry[] entries) : IAgentCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<AgentEntry> All => entries;

    /// <inheritdoc />
    public AgentEntry? Resolve(AgentId id) => entries.FirstOrDefault(e => e.Id == id);
}
