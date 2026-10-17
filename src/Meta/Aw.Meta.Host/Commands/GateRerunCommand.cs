using System.Globalization;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.GateRerun;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'gate-rerun' command: exit 0 for a flake, 1 when the failure is real.</summary>
internal static class GateRerunCommand
{
    internal static CommandEntry Entry { get; } = new(
        "gate-rerun",
        new CommandSpec("gate-rerun <pr> --step <step> --test <filter> [--json]", ["step", "test"], [], [], Positionals: 1),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context) =>
        PrArgument.TryParse(context.Line.Positional(0), out var pr)
            ? CommandOutput.Call<GateRerunRequest, GateRerunResponse>(context, new GateRerunRequest(pr, context.Line.Value("step"), context.Line.Value("test")), Text, static r => r.Verdict == FlakeVerdict.Real)
            : Task.FromResult(PrArgument.Usage(context, Entry));

    private static string Text(GateRerunResponse response) =>
        string.Join(
            Environment.NewLine,
            [
                string.Create(CultureInfo.InvariantCulture, $"{response.Verdict.ToString().ToUpperInvariant()} #{response.Pr} gate run {response.Run} step {response.Step}, test {response.Test}: {response.Reason}"),
                .. response.AloneLog is { } alone ? [$"  alone: {alone}"] : Array.Empty<string>(),
                .. response.MainLog is { } main ? [$"  main:  {main}"] : Array.Empty<string>(),
            ]);
}
