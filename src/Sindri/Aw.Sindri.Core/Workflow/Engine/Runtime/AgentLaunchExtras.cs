using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a Step's launch is given beside its run, target and descriptor.</summary>
/// <param name="Instructions">What the Step is told to do, for the brief; none when it has none.</param>
/// <param name="Workspace">The workspace the Step reads (an existing worktree it works in); null when it reads none.</param>
/// <param name="Bundle">The pack the Step bundles (LN-WF5), resolved; null for none.</param>
public sealed record AgentLaunchExtras(IReadOnlyList<string>? Instructions = null, string? Workspace = null, PackBundle? Bundle = null);
