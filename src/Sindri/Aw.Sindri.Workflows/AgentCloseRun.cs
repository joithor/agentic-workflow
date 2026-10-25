namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'CloseAgentStep' is given (engine P8 D9, D10, D11): the launch to read the usage of and stop, and what the diff signal measures.</summary>
/// <param name="Launch">The launch ('LCH-…').</param>
/// <param name="Worktree">The run's worktree.</param>
/// <param name="StartHead">The commit the worktree started at; null when it couldn't be read.</param>
/// <param name="Measure">Whether to read the usage and the diff signal (an accepted report or an ended session); false only stops the session (a cancel).</param>
internal sealed record AgentCloseRun(string Launch, string? Worktree, string? StartHead, bool Measure);
