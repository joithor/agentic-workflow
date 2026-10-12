using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'workflow' verb group (engine spec rev 8 §6.3). Engine P0 spike: 'complete' only, and it talks to Temporal
/// directly through <see cref="IDeskSteps"/> (no 'serve' route yet); '--temporal host:port' names the dev server's
/// frontend (default 127.0.0.1:7233). Prints what happened: 'T-1/step/1 run 1.1: done -> checks'.
/// </summary>
internal static class WorkflowCommandTable
{
    private const string CompleteUsage =
        "usage: sindri workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--temporal <host:port>] [--no-wait]";

    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["complete", var address, .. var rest] => Complete(address, rest, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["workflow", .. args])}' is not a sindri command ({CompleteUsage})")),
    };

    private static async Task<int> Complete(string address, string[] rest, IServiceProvider services, Output output, CancellationToken ct)
    {
        var (report, problem) = Parse(address, rest);
        if (report is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"{problem} ({CompleteUsage})")).ConfigureAwait(false);
        }

        var done = await services.GetRequiredService<IDeskSteps>().Complete(report, ct).ConfigureAwait(false);
        if (done.IsError)
        {
            return await output.Error(done.Error).ConfigureAwait(false);
        }

        var next = done.Value.State ?? "sent; the instance moves on when a worker runs";
        return await output.Text($"{report.Address.Value} run {report.Run.Value}: {report.Outcome.Value} -> {next}\n", 0).ConfigureAwait(false);
    }

    private static (DeskReport? Report, string? Problem) Parse(string address, string[] rest)
    {
        if (!InstanceAddress.TryFrom(address, out var instance))
        {
            return (null, $"'{address}' is not an instance address ('{{item}}/{{recipe}}/{{attempt}}')");
        }

        var wait = !rest.Contains("--no-wait", StringComparer.Ordinal);
        var pairs = rest.Where(static a => a != "--no-wait").ToArray();
        if (pairs.Length % 2 != 0)
        {
            return (null, $"'{pairs[^1]}' needs a value");
        }

        var options = Enumerable.Range(0, pairs.Length / 2).Select(i => (Flag: pairs[2 * i], Value: pairs[(2 * i) + 1])).ToList();
        if (options.FirstOrDefault(static o => o.Flag is not ("--run" or "--outcome" or "--output" or "--temporal")) is { Flag: not null } unknown)
        {
            return (null, $"'{unknown.Flag}' isn't an option");
        }

        var outputs = Outputs(options.Where(static o => o.Flag == "--output").Select(static o => o.Value));
        return (Single(options, "--run"), Single(options, "--outcome"), outputs) switch
        {
            (null, _, _) => (null, "--run <token> is required"),
            (_, null, _) => (null, "--outcome <o> is required"),
            (_, _, null) => (null, "each --output is <fact>=<file>, with a fact key such as fact:output"),
            ({ } r, _, _) when !RunToken.TryFrom(r, out _) => (null, $"'{r}' is not a run token ('{{entry}}.{{n}}')"),
            (_, { } o, _) when !OutcomeName.TryFrom(o, out _) => (null, $"'{o}' is not an outcome name"),
            ({ } r, { } o, { } files) => (new DeskReport(instance, RunToken.From(r), OutcomeName.From(o), files, Single(options, "--temporal"), wait), null),
        };
    }

    private static string? Single(List<(string Flag, string Value)> options, string flag) =>
        options.LastOrDefault(o => o.Flag == flag).Value;

    private static Dictionary<FactKey, string>? Outputs(IEnumerable<string> values)
    {
        var outputs = new Dictionary<FactKey, string>();
        foreach (var value in values)
        {
            if (value.Split('=', 2) is not [var fact, { Length: > 0 } file] || !FactKey.TryFrom(fact, out var key))
            {
                return null;
            }

            outputs[key] = Path.GetFullPath(file);
        }

        return outputs;
    }
}
