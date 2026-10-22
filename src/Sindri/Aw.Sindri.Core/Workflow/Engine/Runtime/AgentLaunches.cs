using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The pure half of an agent Step's launch (engine P8 D4, D8): its deterministic launch id and the unattended request the engine hands to the launcher.</summary>
public static class AgentLaunches
{
    private static readonly string[] _report = ["Bash(sindri workflow complete:*)", "Bash(sindri workflow status:*)", "Bash(sindri workflow events:*)"];
    private static readonly string[] _evidence = ["Bash(ui-evidence:*)"];
    private static readonly string[] _commit = ["Bash(git add:*)", "Bash(git commit:*)", "Bash(git status:*)", "Bash(git diff:*)"];

    /// <summary>The launch id of a run: 'LCH-' and the first 32 hex digits of SHA-256 of the instance id, ':' and the run token. A retry or a replay can never start a second session.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The id.</returns>
    public static LaunchId LaunchIdFor(AgentStepRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return LaunchIdFor(run.Workflow, run.Run);
    }

    /// <summary>The launch id of a run, from the instance and the run token alone: what a status read names before the launch activity has run.</summary>
    /// <param name="workflow">The instance.</param>
    /// <param name="run">The run token.</param>
    /// <returns>The id.</returns>
    public static LaunchId LaunchIdFor(WorkflowInstanceId workflow, RunToken run)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(workflow.Value + ":" + run.Value));
        return LaunchId.From("LCH-" + Convert.ToHexStringLower(digest)[..32]);
    }

    /// <summary>The worktree the launcher creates for the run: '&lt;repo&gt;/.claude/worktrees/sindri-&lt;instance&gt;-&lt;run&gt;'.</summary>
    /// <param name="run">The run.</param>
    /// <param name="target">Where it runs.</param>
    /// <returns>The path.</returns>
    public static string WorktreeFor(AgentStepRun run, AgentStepTarget target)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(target);
        return target.RepoPath.TrimEnd('/') + "/.claude/worktrees/sindri-" + run.Workflow.Value + "-" + run.Run.Value;
    }

    /// <summary>The launcher's request: unattended, Open, the worktree, the Step's reference and the policy its capability gives.</summary>
    /// <param name="run">The run.</param>
    /// <param name="target">Where it runs.</param>
    /// <param name="descriptor">The Step's descriptor, for the brief; null when the catalog doesn't know it.</param>
    /// <param name="evaluator">Non-null for the holdout evaluator (LN-LCH2): it runs in a scratch checkout at the head sha, reads the holdout set and never writes it, can't touch the phase worktree, and may run the evidence CLIs.</param>
    /// <returns>The request.</returns>
    public static LaunchRequest RequestFor(AgentStepRun run, AgentStepTarget target, BlockDescriptor? descriptor = null, EvaluatorLaunch? evaluator = null)
    {
        if (evaluator is not null)
        {
            // The launcher fills in the writable path: the scratch checkout it makes. No commit rights: the evaluator reports, it doesn't change the repo.
            var evaluatorPolicy = new StepPolicy([.. _report, .. _evidence], StepPermissionMode.Default, WritablePath: null, ReadOnlyPaths: [evaluator.HoldoutPath], DeniedPaths: [evaluator.PhaseWorktree]);
            return new LaunchRequest(
                LaunchIdFor(run), target.Item, target.Agent, LaunchMode.Open, Attended: false, target.RepoPath, run.Address, Worktree: null,
                new StepArgs(Model: null, evaluatorPolicy, run.Step, AgentStepBrief.Render(run, descriptor)), run.Run, Evaluator: evaluator);
        }

        var worktree = WorktreeFor(run, target);
        // Never plan mode: plan mode runs read-only tools only, so the session couldn't run 'sindri workflow complete'. A Step only tightens the
        // profile: every Step may report, a write Step may write its own worktree and commit it, and nothing else is allowed (no build tools: SC-R).
        var policy = new StepPolicy(run.WriteScope ? [.. _report, .. _commit] : _report, StepPermissionMode.Default, run.WriteScope ? worktree : null);
        return new LaunchRequest(
            LaunchIdFor(run), target.Item, target.Agent, LaunchMode.Open, Attended: false, target.RepoPath, run.Address, worktree,
            new StepArgs(Model: null, policy, run.Step, AgentStepBrief.Render(run, descriptor)), run.Run);   // Model null: per-Step models are SC-R
    }
}
