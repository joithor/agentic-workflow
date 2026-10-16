namespace Aw.Meta.Core.Lanes;

/// <summary>What a lane's tmux session shows now.</summary>
/// <param name="Command">The command running in its pane (the agent's process name, or a bare shell).</param>
/// <param name="Clients">How many clients are attached to the session.</param>
public sealed record PaneState(string Command, int Clients);
