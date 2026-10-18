using System.Globalization;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.Launching;

/// <summary>Doctor's launcher rows (launcher spec section 4): the viewer and the config file, each entry's resolved bin and the Steps that would park, and the opens of the last 30 days.</summary>
/// <param name="setup">The box-local catalog and viewer report.</param>
/// <param name="launches">The launches projection.</param>
/// <param name="clock">Now.</param>
internal sealed class LauncherDoctorChecks(ILaunchSetup setup, ILaunchViews launches, TimeProvider clock) : IDoctorChecks
{
    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var shown = await setup.Show(only: null, cancellationToken).ConfigureAwait(false);
        if (shown.IsError)
        {
            return [new DoctorCheck("launcher", CheckState.Fail, shown.Error.Message, "sindri launch show")];
        }

        var report = shown.Value;
        var rows = await launches.Since(clock.GetUtcNow() - LaunchAdoption.Window, cancellationToken).ConfigureAwait(false);
        return [Viewer(report), Agents(report), new DoctorCheck("launcher:adoption", CheckState.Ok, rows.IsError ? "the launches view couldn't be read" : LaunchAdoption.Line(rows.Value), Fix: null)];
    }

    private static DoctorCheck Viewer(LaunchShowReport report) => report.Problems.Count > 0
        ? new DoctorCheck("launcher", CheckState.Warn, string.Join("; ", report.Problems), "sindri launch show")
        : string.Equals(report.TerminalSource, "file", StringComparison.Ordinal)
            ? new DoctorCheck("launcher", CheckState.Ok, $"viewer {report.Terminal} from {report.ConfigPath}", Fix: null)
            : new DoctorCheck("launcher", CheckState.Ok, $"no launcher.json at {report.ConfigPath}; this box's viewer is {report.Terminal} (sindri launch init writes it)", Fix: null);

    private static DoctorCheck Agents(LaunchShowReport report)
    {
        var lines = report.Entries.Select(static e => string.Create(CultureInfo.InvariantCulture, $"{e.Id.Value} ({e.Source.ToString().ToLowerInvariant()}): {e.ResolvedBin ?? "not on the login PATH"}")).ToList();
        var parked = report.Entries.Where(static e => e.ParkLine is not null).Select(static e => e.ParkLine!).ToList();
        var missing = report.Entries.Any(static e => e.ResolvedBin is null);
        var detail = string.Join("; ", lines) + (parked.Count == 0 ? string.Empty : "; " + string.Join("; ", parked));
        return new DoctorCheck("launcher:agents", missing || parked.Count > 0 ? CheckState.Warn : CheckState.Ok, detail, missing || parked.Count > 0 ? "sindri launch show" : null);
    }
}
