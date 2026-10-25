namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The agent runtime's knobs (engine P8 D3): how often the watch asks about the session. Tests give it a shorter interval.</summary>
/// <param name="PollInterval">How often 'WatchAgentStep' reads the session's state and heartbeats; 5 s in production.</param>
internal sealed record AgentRuntimeOptions(TimeSpan PollInterval)
{
    /// <summary>The production options.</summary>
    public static AgentRuntimeOptions Default { get; } = new(TimeSpan.FromSeconds(5));
}
