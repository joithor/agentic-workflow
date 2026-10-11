using System.Text.Json;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Usage;
using Aw.Evidence.Core.Visual;
using Aw.Evidence.Core.Files;

namespace Aw.Evidence.Features.RunScript;

/// <summary>
/// The run's visual verdict through the visual gate: the last passed step's screenshot against the baseline, then the
/// verdict cache, then the judge. No passed step leaves the verdict unchecked. The cache is saved best-effort.
/// </summary>
internal sealed class RunVerdict(VisualGate gate, IVerdictCacheStore store, ISecretsSource settings, JsonSerializerOptions json)
{
    internal const string ModelVariable = "AW_VISUAL_MODEL";
    internal const string DefaultModel = "judge-default";
    internal const string CacheFile = "verdict-cache.json";

    internal async ValueTask<RunVisual> DecideAsync(RunScriptRequest request, UiScript script, DrivenRun driven, InvocationLog log, CancellationToken cancellationToken)
    {
        var lastPassed = driven.Steps.LastOrDefault(static s => s.Status == StepStatus.Passed);
        if (lastPassed is null)
        {
            return new RunVisual(Verdict.Unchecked, [], DiffScore: null, DiffImage: null);
        }

        var cachePath = request.CacheManifest ?? RunPath.Combine(request.RunDirectory, CacheFile);
        var cache = await store.LoadAsync(cachePath, cancellationToken).ConfigureAwait(false);
        var context = new VerdictCacheContext(
            VerdictCacheContext.CurrentPromptVersion,
            settings.Get(ModelVariable) ?? DefaultModel,
            request.AppBuild,
            VerdictCacheContext.ScenarioOf(script, json),
            request.Fixtures,
            string.Join(',', script.Viewports.Select(ScriptViewports.NameOf)),
            driven.BrowserVersion);
        var input = new VisualGateInput(
            lastPassed.Screenshot,
            request.Baseline,
            driven.Steps.Any(static s => s.Status != StepStatus.Passed),
            RunId.From(RunPath.NameOf(request.RunDirectory)),
            request.RunDirectory,
            context,
            cache);
        var outcome = await gate.DecideAsync(input, log, cancellationToken).ConfigureAwait(false);
        _ = await store.SaveAsync(cachePath, outcome.Cache, cancellationToken).ConfigureAwait(false);   // a cache is never worth failing a run
        return new RunVisual(outcome.Verdict, outcome.Reasons, outcome.DiffScore, outcome.DiffImage);
    }
}
