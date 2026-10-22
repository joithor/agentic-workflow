using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Where and as whom an agent Step runs (engine P8 D8): the item, the profile's 'workflow.agent.target' and the item's repo checkout.</summary>
/// <param name="Item">The item the session works on.</param>
/// <param name="Agent">The catalog entry to run.</param>
/// <param name="RepoPath">The repo checkout the worktree is made under.</param>
/// <param name="Repo">The repo's name in the profile, for the pack a Step bundles (LN-WF5); null when the item names none a typed name fits.</param>
public sealed record AgentStepTarget(ItemId Item, AgentId Agent, string RepoPath, RepoName? Repo = null);
