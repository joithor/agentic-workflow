using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Gathers the facts the merge gate decides on (the recorded gates, GitHub's view of the pull request, origin/main, the earlier
/// pull requests it must wait for) and runs MergeGate over them. A pull request that was never gated is refused without asking GitHub.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="gates">The recorded gate runs.</param>
/// <param name="repo">The git and gh calls.</param>
public sealed class MergeReadiness(IMetaConfig config, IGateStore gates, RepoCommands repo)
{
    private const string Fields = "headRefOid,isDraft,title,state";

    /// <summary>Checks a pull request.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The checks, or an error from the store, git or gh.</returns>
    public async ValueTask<Result<MergeAssessment>> Assess(int pr, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var history = await gates.History(pr, cancellationToken).ConfigureAwait(false);
        if (team.IsError || history.IsError)
        {
            return team.IsError ? team.Error : history.Error;
        }

        if (history.Value.Count == 0)
        {
            return new MergeAssessment(team.Value, MergeGate.Evaluate(new MergeFacts { Pr = pr, CurrentHead = string.Empty, CurrentMain = string.Empty }), Pr: null);
        }

        var current = await Current(team.Value.Repo, pr).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var (view, main) = current.Value;
        var (gate, repoPassed) = Choose(history.Value, view.Head, team.Value.Gate.Always);
        var facts = new MergeFacts
        {
            Pr = pr,
            Gate = gate,
            CurrentHead = view.Head,
            CurrentMain = main,
            IsDraft = view.IsDraft,
            DeltaPaths = await Delta(team.Value.Repo, gate, view.Head).ConfigureAwait(false),
            RepoGatePassedOnHead = repoPassed,
            OpenPrs = await OpenBefore(team.Value, pr).ConfigureAwait(false),
            MergeOrder = team.Value.MergeOrder,
            DocsOnly = team.Value.Gate.DocsOnly,
            PlannedScopes = await Planned(team.Value, gate).ConfigureAwait(false),
        };
        return new MergeAssessment(team.Value, [.. MergeGate.Evaluate(facts), State(view)], view);
    }

    private async ValueTask<Result<(PrDetails View, string Main)>> Current(string repoDirectory, int pr)
    {
        var fetched = await repo.Fetch(repoDirectory, pr).ConfigureAwait(false);
        var view = fetched.IsError ? Results.Failure<PrDetails>(fetched.Error) : await repo.View(repoDirectory, pr, Fields).ConfigureAwait(false);
        var main = view.IsError ? Results.Failure<string>(view.Error) : await repo.Sha(repoDirectory, "origin/main").ConfigureAwait(false);
        return main.IsError ? main.Error : (view.Value, main.Value);
    }

    // The gate the merge rests on. When the latest run was only the always scopes (a re-check after a docs commit), it vouches
    // for the new head but not for the code: the newest run that went beyond them is the one the head is measured from.
    private static (GateRecord Gate, bool RepoPassedOnHead) Choose(IReadOnlyList<GateRecord> history, string head, IReadOnlyList<string> always)
    {
        var latest = history[0];
        var beyond = history.FirstOrDefault(g => g.Scopes.Any(scope => !always.Contains(scope, StringComparer.Ordinal)));
        return beyond is not null && beyond.Run != latest.Run && latest.Scopes.All(scope => always.Contains(scope, StringComparer.Ordinal))
            ? (beyond, latest.Head == head && latest.Verdict == GateVerdict.Pass)
            : (latest, false);
    }

    private async ValueTask<IReadOnlyList<string>?> Delta(string repoDirectory, GateRecord gate, string head)
    {
        if (gate.Head == head)
        {
            return null;
        }

        var changed = await repo.Changed(repoDirectory, $"{gate.Head}..{head}").ConfigureAwait(false);
        return changed.IsSuccess ? changed.Value : null;
    }

    // The scopes the gated code needs, from the same planner gate-run uses, over the diff the gate merged.
    private async ValueTask<IReadOnlyList<string>?> Planned(TeamConfig team, GateRecord gate)
    {
        var changed = await repo.Changed(team.Repo, $"{gate.Main}...{gate.Head}").ConfigureAwait(false);
        return changed.IsSuccess ? ScopePlanner.Plan(changed.Value, team.Gate) : null;
    }

    private async ValueTask<IReadOnlyList<int>> OpenBefore(TeamConfig team, int pr)
    {
        var open = new List<int>();
        foreach (var rule in team.MergeOrder.Where(rule => rule.After == pr))
        {
            var earlier = await repo.View(team.Repo, rule.Before, "state").ConfigureAwait(false);
            if (earlier.IsError || earlier.Value.State == PrState.Open)
            {
                open.Add(rule.Before);   // when it can't be read, assume it is still open: the safe side
            }
        }

        return open;
    }

    private static MergeCheck State(PrDetails view) =>
        new("state", view.State == PrState.Open, view.State == PrState.Open ? "open" : view.State.ToString().ToLowerInvariant());
}
