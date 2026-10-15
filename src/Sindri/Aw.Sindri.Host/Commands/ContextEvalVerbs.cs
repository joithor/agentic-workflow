using Aw.Context.Core;
using Aw.Context.Core.Eval;
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
/// worse than the previous run of the same file; 1 when it regressed (SND-CONTEXT-038) or the file is refused; 2 for a
/// tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="engine">The context engine, which reads the file, scores and writes the report.</param>
internal sealed class ContextEvalVerbs(ControlPlaneClient client, Output output, IContextEngine engine)
{
    internal Task<int> Run(string[] args, CancellationToken ct) => args switch
    {
        ["probes", .. var rest] when Options(rest) is { } o => Probes(o.File, o.Json, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'context eval {string.Join(' ', args)}' is not an eval command: context eval probes [--file <probes.json>] [--json]")),
    };

    private async Task<int> Probes(string? file, bool json, CancellationToken ct)
    {
        var run = await engine.EvalProbes(file is null ? null : System.IO.Path.GetFullPath(file), Ask, ct).ConfigureAwait(false);
        if (run.IsError)
        {
            return await Failed(run.Error).ConfigureAwait(false);
        }

        var exit = run.Value.Report.Regressed ? 1 : 0;
        return json
            ? await output.Json(run.Value.Report, exit).ConfigureAwait(false)
            : await output.Text(ContextEvalText.Report(run.Value.Report, run.Value.Path), exit).ConfigureAwait(false);
    }

    private async ValueTask<Result<ContextResult>> Ask(ProbeCase probe, CancellationToken ct)
    {
        var answer = await client.Get<ContextResult>("/sindri/context/query?q=" + Uri.EscapeDataString(probe.Question), ct).ConfigureAwait(false);
        return answer.IsError ? answer.Error : answer.Value.Value;
    }

    private async Task<int> Failed(Error error)
    {
        var code = await output.Error(error).ConfigureAwait(false);
        return error.Code == "SND-API-001" || ContextErrors.Attention.Contains(error.Code) ? 1 : code;
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
