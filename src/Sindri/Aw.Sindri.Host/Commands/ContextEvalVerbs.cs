using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context eval probes [--file F] [--json]' (context spec §8.5, §9.2 item 3). No route: the CLI asks each probe's
/// question through GET /sindri/context/query as whoever runs it; the engine (EvalProbes) reads the file, scores the
/// answers and writes the report under $AW_SINDRI_HOME/context/eval/probes/ (0600). Exit 0 when the run is no
/// worse than the baseline (the newest complete run of the same file that did not regress); 1 when it regressed
/// (SND-CONTEXT-038) or the file is refused; 2 when any probe's query failed, every one included (the run is incomplete:
/// the failed probes are listed and nothing is saved) or for a tool error. --json prints the run: {report, path, incomplete, failed}.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="engine">The context engine, which reads the file, scores and writes the report.</param>
internal sealed class ContextEvalVerbs(ControlPlaneClient client, Output output, IContextEngine engine)
{
    internal Task<int> Run(string[] args, CancellationToken ct) => args switch
    {
        ["probes", .. var rest] when Options(rest) is { } o => Probes(o.File, o.Json, ct),
        ["map", .. var rest] when rest is [] or ["--json"] => Map(rest.Length == 1, ct),
        ["purge", .. var rest] when rest is [] or ["--all"] => Purge(rest.Length == 1, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'context eval {string.Join(' ', args)}' is not an eval command: context eval probes [--file <probes.json>] [--json] | map [--json] | purge [--all] | backtest [--arms a,b|all] [--since D] [--until D] [--limit N] [--detach] [--json]")),
    };

    private async Task<int> Map(bool json, CancellationToken ct)
    {
        var run = await client.Get<MapQualityRun>("/sindri/context/eval/map", ct).ConfigureAwait(false);
        if (run.IsError)
        {
            return await Failed(run.Error).ConfigureAwait(false);
        }

        return json ? await output.Raw(run.Value.Body, 0).ConfigureAwait(false) : await output.Text(ContextEvalText.Map(run.Value.Value), 0).ConfigureAwait(false);
    }

    /// <summary>Runs in this process (as 'eval probes' does): it deletes report files under $AW_SINDRI_HOME, so it needs no route. For whoever runs it, a session included.</summary>
    private async Task<int> Purge(bool all, CancellationToken ct)
    {
        var scope = new ContextScope(Settings: null, [], new ContextCaller(await client.ResolveSessionReason(ct).ConfigureAwait(false) is null ? ContextCallerKind.Human : ContextCallerKind.Session, Repo: null, ContextClient.Cli));
        var purged = await engine.EvalPurge(new EvalPurgeRequest(scope, all), ct).ConfigureAwait(false);
        return purged.IsError ? await Failed(purged.Error).ConfigureAwait(false) : await output.Text(ContextEvalText.Purge(purged.Value, all), 0).ConfigureAwait(false);
    }

    private async Task<int> Probes(string? file, bool json, CancellationToken ct)
    {
        var run = await engine.EvalProbes(file is null ? null : Path.GetFullPath(file), Ask, ct).ConfigureAwait(false);
        if (run.IsError)
        {
            return await Failed(run.Error).ConfigureAwait(false);
        }

        var exit = Exit(run.Value);
        return json
            ? await output.Json(run.Value, exit).ConfigureAwait(false)
            : await output.Text(ContextEvalText.Report(run.Value.Report, run.Value.Path), exit).ConfigureAwait(false);
    }

    /// <summary>The exit code of a finished run: 0 no worse, 1 regressed, 2 incomplete (a probe's query failed; nothing was saved).</summary>
    /// <param name="run">The run.</param>
    /// <returns>The code.</returns>
    internal static int Exit(ProbeRun run) => run.Report.Incomplete ? 2 : run.Report.Regressed ? 1 : 0;

    private async ValueTask<Result<ContextResult>> Ask(ProbeCase probe, CancellationToken ct)
    {
        var answer = await client.Get<ContextResult>("/sindri/context/query?q=" + Uri.EscapeDataString(probe.Question), ct).ConfigureAwait(false);
        return answer.IsError ? answer.Error : answer.Value.Value;
    }

    private async Task<int> Failed(Error error)
    {
        var code = await output.Error(error).ConfigureAwait(false);
        return ContextErrors.Attention.Contains(error.Code) ? 1 : code;
    }

    /// <summary>'[--file F] [--json]' in either order.</summary>
    private static (string? File, bool Json)? Options(string[] rest) => rest switch
    {
        [] => (null, false),
        ["--json"] => (null, true),
        ["--file", var f] => (f, false),
        ["--file", var f, "--json"] => (f, true),
        ["--json", "--file", var f] => (f, true),
        _ => null,
    };
}
