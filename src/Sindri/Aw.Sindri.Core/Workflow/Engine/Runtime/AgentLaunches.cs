using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The pure half of an agent Step's launch (engine P8 D4, D8): its deterministic launch id and the unattended request the engine hands to the launcher.</summary>
public static class AgentLaunches
{
    /// <summary>The launch id of a run: 'LCH-' and the first 32 hex digits of SHA-256 of the instance id, ':' and the run token. A retry or a replay can never start a second session.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The id.</returns>
    public static LaunchId LaunchIdFor(AgentStepRun run) => throw new NotSupportedException("AgentLaunches.LaunchIdFor is not built yet");

    /// <summary>The worktree the launcher creates for the run: '&lt;repo&gt;/.claude/worktrees/sindri-&lt;instance&gt;-&lt;run&gt;'.</summary>
    /// <param name="run">The run.</param>
    /// <param name="target">Where it runs.</param>
    /// <returns>The path.</returns>
    public static string WorktreeFor(AgentStepRun run, AgentStepTarget target) => throw new NotSupportedException("AgentLaunches.WorktreeFor is not built yet");

    /// <summary>The launcher's request: unattended, Open, the worktree, the Step's reference and the policy its capability gives.</summary>
    /// <param name="run">The run.</param>
    /// <param name="target">Where it runs.</param>
    /// <returns>The request.</returns>
    public static LaunchRequest RequestFor(AgentStepRun run, AgentStepTarget target) => throw new NotSupportedException("AgentLaunches.RequestFor is not built yet");
}
