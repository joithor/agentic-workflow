namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'CloseAgentStep' returns (engine P8 D9, D10).</summary>
/// <param name="Tokens">The tokens the session spent, clamped to what a trigger carries.</param>
/// <param name="EmptyDiff">Whether the engine measured an empty diff: HEAD still at the start and a clean worktree. False when it was not measured.</param>
internal sealed record AgentClosed(int Tokens, bool EmptyDiff);
