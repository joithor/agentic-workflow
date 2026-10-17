using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// What <see cref="ArgvBuilder.Build"/> expands an entry's templates with (launcher spec section 3): ids, the repo path, the session name,
/// the seed (a fixed template plus ids, UI 6.2) and the per-launch MCP config path. Never item text.
/// </summary>
/// <param name="Item">The item the session works on.</param>
/// <param name="RepoPath">The directory the session starts in ('{cwd}'); must be absolute.</param>
/// <param name="SessionName">The session's name ('{name}' and '{title}'), such as 'sindri ITM-…'.</param>
/// <param name="Seed">The seed prompt ('{seed}'); <see cref="ArgvBuilder.Seed"/> builds the fixed one.</param>
/// <param name="SessionId">The session id ('{sessionId}'): the pre-chosen UUID on Open, the stored id on Resume; null when there is none.</param>
/// <param name="McpConfigPath">The per-launch MCP config file ('{mcpConfig}'), or null.</param>
/// <param name="Step">What an unattended Step adds; null for a human open. The L-17 forms apply it later (P10a U3), not yet.</param>
public sealed record ArgvInputs(
    ItemId Item,
    string RepoPath,
    string SessionName,
    string Seed,
    Sessions.SessionId? SessionId = null,
    string? McpConfigPath = null,
    StepArgs? Step = null);
