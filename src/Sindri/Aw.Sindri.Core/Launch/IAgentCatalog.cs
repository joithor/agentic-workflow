namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The agent catalog (launcher spec SL3): the built-in entries merged with the ones in launcher.json. The session collector
/// uses it as its provider list (collector spec CO13); SL-2 extends it with the launch argv.
/// </summary>
public interface IAgentCatalog
{
    /// <summary>Every entry, built-ins first.</summary>
    public IReadOnlyList<AgentEntry> All { get; }

    /// <summary>The entry named <paramref name="id"/>.</summary>
    /// <param name="id">The agent id.</param>
    /// <returns>The entry, or null when the catalog has none.</returns>
    public AgentEntry? Resolve(AgentId id);
}
