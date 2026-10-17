using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.GateRerun;

/// <summary>
/// Reruns one test of a red gate step alone on the gate tree and, when it still fails, on a plain-main tree, then stores
/// whether the failure is a flake or real on the gate's step. A known flake (the config's list) is a flake whatever else is found.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="gates">The gate runs.</param>
/// <param name="workspace">The gate and main trees.</param>
/// <param name="reruns">Runs the test.</param>
/// <param name="logs">Reads the gate step's log for its failure text.</param>
internal sealed class GateRerunService(IMetaConfig config, IGateStore gates, GateWorkspace workspace, RerunExecutor reruns, ILogFiles logs)
    : IService<GateRerunRequest, GateRerunResponse>
{
    private const int LogCharacters = 512 * 1024;

    public async ValueTask<Result<GateRerunResponse>> Handle(GateRerunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var latest = await gates.Latest(request.Pr, cancellationToken).ConfigureAwait(false);
        if (team.IsError || latest.IsError)
        {
            return team.IsError ? team.Error : latest.Error;
        }

        var target = Resolve(request, team.Value, latest.Value);
        if (target.IsError)
        {
            return target.Error;
        }

        var judged = await Judge(team.Value, target.Value, request, cancellationToken).ConfigureAwait(false);
        if (judged.IsError)
        {
            return judged.Error;
        }

        var run = target.Value.Gate.Run;
        var stored = await gates.Classify(request.Pr, run, request.Step, new TestVerdict(request.Test, judged.Value.Verdict), cancellationToken).ConfigureAwait(false);
        return stored.IsError ? stored.Error : new GateRerunResponse(request.Pr, run, request.Step, request.Test, judged.Value.Verdict, judged.Value.Reason) { AloneLog = judged.Value.AloneLog, MainLog = judged.Value.MainLog };
    }

    private Result<RerunTarget> Resolve(GateRerunRequest request, TeamConfig team, GateRecord? gate)
    {
        var configured = team.Gate.Scopes.Values.SelectMany(scope => scope.Steps).FirstOrDefault(step => step.Name == request.Step);
        if (configured is null)
        {
            return MetaErrors.Invalid.With($"no gate step '{request.Step}' in the team config");
        }

        if (gate is null)
        {
            return MetaErrors.Invalid.With(string.Create(CultureInfo.InvariantCulture, $"no gate recorded for #{request.Pr}; run aw-meta gate-run {request.Pr} first"));
        }

        var recorded = gate.Steps.FirstOrDefault(step => step.Name == request.Step);
        if (recorded is null)
        {
            return MetaErrors.GateStepNotFound.With(string.Create(CultureInfo.InvariantCulture, $"gate run {gate.Run} did not run step '{request.Step}'"));
        }

        if (recorded.Verdict == GateVerdict.Pass)
        {
            return MetaErrors.Invalid.With(string.Create(CultureInfo.InvariantCulture, $"step '{request.Step}' passed in gate run {gate.Run}; there is nothing to classify"));
        }

        return reruns.Command(configured, request.Test) is { } command
            ? new RerunTarget(gate, recorded, command)
            : MetaErrors.Invalid.With($"step '{request.Step}' has no way to run one test; give it a \"rerun\" argument vector with {{test}} in the team config");
    }

    private async ValueTask<Result<Judgement>> Judge(TeamConfig team, RerunTarget target, GateRerunRequest request, CancellationToken cancellationToken)
    {
        var aloneLog = workspace.RerunLogPath(request.Pr, target.Gate.Run, request.Step, request.Test, "alone");
        var alone = await reruns.Run(new RerunJob(target.Command, workspace.TreePath(request.Pr), request.Test, FilterMustMatch: true, aloneLog), cancellationToken).ConfigureAwait(false);
        return alone.IsError || alone.Value.Passed
            ? alone.IsError ? alone.Error : new Judgement(FlakeVerdict.Flake, "passes alone on the gate tree", aloneLog)
            : await AgainstMain(team, target, request, alone.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Judgement>> AgainstMain(TeamConfig team, RerunTarget target, GateRerunRequest request, RerunOutcome alone, CancellationToken cancellationToken)
    {
        var aloneLog = workspace.RerunLogPath(request.Pr, target.Gate.Run, request.Step, request.Test, "alone");
        var main = await workspace.PrepareMain(team, request.Pr, target.Gate.Main).ConfigureAwait(false);
        var mainLog = workspace.RerunLogPath(request.Pr, target.Gate.Run, request.Step, request.Test, "main");
        var onMain = main.IsError ? Results.Failure<RerunOutcome>(main.Error) : await reruns.Run(new RerunJob(RerunExecutor.Building(target.Command), main.Value, request.Test, FilterMustMatch: false, mainLog), cancellationToken).ConfigureAwait(false);
        var gateLog = await logs.Tail(target.Step.Log, LogCharacters, cancellationToken).ConfigureAwait(false);
        if (onMain.IsError)
        {
            return onMain.Error;
        }

        var verdict = FlakeRule.Classify(alone, onMain.Value, FailureExcerpt.For(gateLog.IsSuccess ? gateLog.Value : string.Empty, request.Test));
        if (verdict == FlakeVerdict.Real && Known(team, request.Test) is { } known)
        {
            return new Judgement(FlakeVerdict.Flake, $"known flake ({known}); it also fails alone (output: {aloneLog})", aloneLog, mainLog);
        }

        return new Judgement(verdict, Reason(verdict, onMain.Value) + $" (alone: {aloneLog}; main: {mainLog})", aloneLog, mainLog);
    }

    private static string Reason(FlakeVerdict verdict, RerunOutcome onMain) =>
        verdict == FlakeVerdict.Flake ? "fails alone the way it fails on main"
        : onMain.Absent ? "fails alone; the test is not on main"
        : onMain.Passed ? "fails alone and passes on main"
        : "fails alone, and not the way it fails on main";

    private static string? Known(TeamConfig team, string test) =>
        team.KnownFlakes.FirstOrDefault(flake => TestNames.Matches(flake, test));
}
