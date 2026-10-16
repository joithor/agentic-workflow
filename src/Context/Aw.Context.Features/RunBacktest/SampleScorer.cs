using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Map;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.RunBacktest;

/// <summary>Scores every sample on every running arm (spec §9.1): the brief, the as-of map and docs at the cut, then each arm through the planner.</summary>
internal sealed class SampleScorer(IBriefSource briefs, AsOfMapBuilder maps, IAsOfDocs docs, ArmRunner arms)
{
    /// <summary>Scores the window.</summary>
    /// <param name="request">The request (arms, scope, progress).</param>
    /// <param name="world">The call's settings, repos and scrubbers.</param>
    /// <param name="loaded">The corpus.</param>
    /// <param name="cancellationToken">Cancels the run; the exception propagates and no report is written.</param>
    /// <returns>The scores, or SND-CONTEXT-011 when no brief could be read at all.</returns>
    public async ValueTask<Result<Collected>> Score(BacktestRequest request, BriefScope world, LoadedCorpus loaded, CancellationToken cancellationToken)
    {
        var running = request.Arms.Where(a => Arms.PendingStage(a) is null).ToList();
        var kept = new List<CorpusSample>();
        var scores = running.ToDictionary(a => a, _ => new List<SampleScore>());
        var dropped = new Dictionary<Outcome, int>();
        for (var i = 0; i < loaded.Samples.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sample = loaded.Samples[i];
            var step = string.Create(CultureInfo.InvariantCulture, $"sample {i + 1}/{loaded.Samples.Count} {sample.IssueKey}");
            var outcome = await One(request, (world, loaded.Issues[sample.IssueKey], sample), (running, scores, step), cancellationToken).ConfigureAwait(false);
            if (outcome == Outcome.Scored)
            {
                kept.Add(sample);
            }

            dropped[outcome] = dropped.GetValueOrDefault(outcome) + 1;
        }

        return dropped.GetValueOrDefault(Outcome.SourceDown) > 0 && kept.Count == 0
            ? ContextErrors.AllSourcesUnavailable.With("the brief source answered no brief; fix: sindri context sources status")
            : new Collected(kept, scores, dropped.GetValueOrDefault(Outcome.CutUnreadable), dropped.GetValueOrDefault(Outcome.NoBrief));
    }

    private async ValueTask<Outcome> One(BacktestRequest request, (BriefScope World, BriefIssue Issue, CorpusSample Sample) what,
        (List<Arm> Running, Dictionary<Arm, List<SampleScore>> Scores, string Step) how, CancellationToken cancellationToken)
    {
        var (world, issue, sample) = what;
        var brief = await briefs.Brief(world, issue, cancellationToken).ConfigureAwait(false);
        if (brief.IsError || brief.Value is null)
        {
            request.Progress?.Report($"{how.Step}: dropped, no brief");
            return brief.IsError ? Outcome.SourceDown : Outcome.NoBrief;
        }

        var opened = await Open(world.Repos.First(r => r.Name == sample.Repo), sample, world, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            request.Progress?.Report($"{how.Step}: dropped, git can't read the cut");
            return Outcome.CutUnreadable;
        }

        request.Progress?.Report($"{how.Step}: map {(opened.Value.Graph.Cached ? "cached" : "built")}");
        await Ask(request, (how.Running, sample, how.Scores), (brief.Value.Text, opened.Value), cancellationToken).ConfigureAwait(false);
        return Outcome.Scored;
    }

    private async ValueTask<Result<Opened>> Open(ContextRepo repo, CorpusSample sample, BriefScope world, CancellationToken cancellationToken)
    {
        var built = await maps.Build(new AsOfRequest(repo, sample), cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var source = await docs.Open(new AsOfDocsRequest(repo, sample.Cut, world.Settings.DocGlobs, world.Scrubbers), cancellationToken).ConfigureAwait(false);
        return source.IsError ? source.Error : new Opened(built.Value, source.Value);
    }

    private async ValueTask Ask(BacktestRequest request, (List<Arm> Running, CorpusSample Sample, Dictionary<Arm, List<SampleScore>> Scores) run, (string Brief, Opened Opened) text,
        CancellationToken cancellationToken)
    {
        var (running, sample, scores) = run;
        var graph = text.Opened.Graph.Graph;
        var truth = BacktestScoring.TruthNodes(graph, sample.TruthFiles);
        var visible = new VisibleMap(new MapSnapshot(new MapVersion(0, sample.Repo, sample.Cut, KeyVersion: 1, sample.CreatedAt, MapVersionStatus.Current), graph, []), graph);
        var inputs = new ArmInputs(request.Scope, sample, Question(text.Brief), visible, text.Opened.Docs);
        foreach (var arm in running)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await arms.Run(arm, inputs, cancellationToken).ConfigureAwait(false);
            scores[arm].Add(BacktestScoring.Score(sample, Arms.Name(arm), truth, result, armLeaky: arm == Arm.GraphReviewed));
            request.Progress?.Report($"arm {Arms.Name(arm)} {sample.IssueKey}");
        }
    }

    private static string Question(string brief)
    {
        var clean = new string([.. brief.Where(c => !char.IsControl(c) || c is '\n' or '\t')]);
        return clean.Length <= QueryRules.MaxTextLength ? clean : clean[..QueryRules.MaxTextLength];
    }

    private enum Outcome
    {
        Scored,
        NoBrief,
        SourceDown,
        CutUnreadable,
    }

    private sealed record Opened(AsOfGraph Graph, IContextSource Docs);
}
