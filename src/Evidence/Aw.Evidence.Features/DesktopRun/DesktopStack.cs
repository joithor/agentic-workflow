using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Files;
using Aw.Kernel;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>
/// Builds the seeded data with the repo's fixture command in a scratch directory, captures every screen at every size with its capture
/// command, and tears the data down again, even after a failure. A command that fails ends the run as errored, with the tail of its stderr.
/// </summary>
internal sealed class DesktopStack(IDesktopCommands commands, IScratchDirectories scratch, IRunFiles files)
{
    private const string ScratchPrefix = "aw-desktop-";
    private const int TailLines = 3;
    private const int TailLimit = 300;

    internal async ValueTask<Result<Unit>> RunAsync(DesktopPlan plan, CancellationToken cancellationToken)
    {
        var home = scratch.Create(ScratchPrefix);
        if (home.IsError)
        {
            return home.Error;
        }

        var environment = new Dictionary<string, string>(plan.Settings.Env, StringComparer.Ordinal) { [plan.Settings.HomeEnv] = home.Value };
        try
        {
            return await UpAndCaptureAsync(plan, environment, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DownAsync(plan, environment).ConfigureAwait(false);
            scratch.Remove(home.Value);
        }
    }

    private async ValueTask<Result<Unit>> UpAndCaptureAsync(DesktopPlan plan, Dictionary<string, string> environment, CancellationToken cancellationToken)
    {
        foreach (var step in Steps(plan))
        {
            var ran = await RunCommandAsync(plan, [.. plan.Settings.Fixture.Command, .. step], environment, $"fixture {string.Join(' ', step)}", cancellationToken).ConfigureAwait(false);
            if (ran.IsError)
            {
                return ran;
            }
        }

        foreach (var size in plan.Sizes)
        {
            var captured = await CaptureAsync(plan, size, environment, cancellationToken).ConfigureAwait(false);
            if (captured.IsError)
            {
                return captured;
            }
        }

        return Unit.Value;
    }

    private async ValueTask<Result<Unit>> CaptureAsync(DesktopPlan plan, Core.Geometry.ViewportSize size, Dictionary<string, string> environment, CancellationToken cancellationToken)
    {
        List<(string Route, string Path)> shots = [];
        foreach (var screen in plan.At(size))
        {
            var target = plan.FilesOf(screen, size).App;
            var made = await files.CreateDirectoryAsync(target[..target.LastIndexOf('/')], cancellationToken).ConfigureAwait(false);
            if (made.IsError)
            {
                return made;
            }

            shots.Add((screen.Route, target));
        }

        var command = DesktopCommandLine.Capture(plan.Settings.Capture.Command, size.Describe(), shots);
        return await RunCommandAsync(plan, command, environment, $"capture at {size.Describe()}", cancellationToken).ConfigureAwait(false);
    }

    // The config's steps, then each screen's own, once each and in order of first use.
    private static List<IReadOnlyList<string>> Steps(DesktopPlan plan)
    {
        List<IReadOnlyList<string>> steps = [.. plan.Settings.Fixture.Up];
        foreach (var step in plan.Screens.SelectMany(static s => s.Fixture ?? []))
        {
            if (!steps.Exists(known => known.SequenceEqual(step, StringComparer.Ordinal)))
            {
                steps.Add(step);
            }
        }

        return steps;
    }

    // Teardown never takes the run's cancellation: a cancelled run still stops what it started (its command has its own time limit).
    private async ValueTask DownAsync(DesktopPlan plan, Dictionary<string, string> environment)
    {
        if (plan.Settings.Fixture.Down is { Count: > 0 } down)
        {
            _ = await RunCommandAsync(plan, [.. plan.Settings.Fixture.Command, .. down], environment, "fixture down", CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<Unit>> RunCommandAsync(DesktopPlan plan, IReadOnlyList<string> argv, Dictionary<string, string> environment, string what, CancellationToken cancellationToken)
    {
        var ran = await commands.RunAsync(new CommandSpec(argv, plan.Repo, environment, plan.Settings.Timeout), cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return EvidenceErrors.RunFailed.With($"{what}: {ran.Error.Message}");
        }

        return ran.Value.ExitCode == 0
            ? Unit.Value
            : EvidenceErrors.RunFailed.With(string.Create(CultureInfo.InvariantCulture, $"{what} exited {ran.Value.ExitCode}: {Tail(ran.Value.StandardError)}"));
    }

    private static string Tail(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tail = string.Join(" | ", lines.TakeLast(TailLines));
        return tail.Length <= TailLimit ? tail : "…" + tail[^TailLimit..];
    }
}
