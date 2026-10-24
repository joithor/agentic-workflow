using System.Globalization;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Evidence;

/// <summary>
/// Doctor's 'setup:ui-evidence' line (cutover spec §3.1): Sindri ships the ui-evidence binary next to itself, so the line finds it there (or where AW_UI_EVIDENCE_BIN points) and asks it for 'version'.
/// A missing binary or one that does not answer is a warning, never a failure. It covers the binary only; a missing browser is reported by 'ui-evidence doctor'.
/// </summary>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="sindriPath">The running sindri binary's path, whose folder holds ui-evidence; null when unknown.</param>
/// <param name="runner">Runs the binary.</param>
internal sealed class UiEvidenceDoctorChecks(Func<string, string?> environment, string? sindriPath, IToolRunner runner) : IDoctorChecks
{
    internal const string Name = "setup:ui-evidence";

    private const string Fix = "run ./setup.sh from the agentic-workflow checkout (the evidence tool ships with Sindri)";
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(10);

    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var binary = Locate();
        if (binary is null || !File.Exists(binary))
        {
            return Done(new DoctorCheck(Name, CheckState.Warn, $"ui-evidence not found ({binary ?? "no AW_UI_EVIDENCE_BIN and no sindri path to look beside"})", Fix));
        }

        var run = runner.Run(binary, ["version"], _budget, cancellationToken: cancellationToken);
        if (run is not { Outcome: ToolOutcome.Exited, ExitCode: 0 })
        {
            return Done(new DoctorCheck(Name, CheckState.Warn, string.Create(CultureInfo.InvariantCulture, $"{binary} did not answer 'version' ({run.Outcome.ToString()}, exit {run.ExitCode})"), Fix));
        }

        var stamp = run.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "answers version";
        return Done(new DoctorCheck(Name, CheckState.Ok, stamp, Fix: null));
    }

    private static ValueTask<IReadOnlyList<DoctorCheck>> Done(DoctorCheck check) => ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([check]);

    private string? Locate() =>
        environment("AW_UI_EVIDENCE_BIN") is { Length: > 0 } given ? given
        : Path.GetDirectoryName(sindriPath) is { Length: > 0 } folder ? Path.Combine(folder, "ui-evidence")
        : null;
}
