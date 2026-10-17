using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The agent catalog: the built-in entries merged with launcher.json's (collector spec CA-0).</summary>
internal sealed class AgentCatalog(IReadOnlyList<AgentEntry> entries) : IAgentCatalog
{
    public IReadOnlyList<AgentEntry> All { get; } = entries;

    public AgentEntry? Resolve(AgentId id) => All.FirstOrDefault(e => e.Id.Equals(id));
}
