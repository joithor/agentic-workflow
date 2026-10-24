using System.Text.RegularExpressions;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Data;

/// <summary>The checks of the targets config's 'desktop' part.</summary>
internal static partial class DesktopTargetsRules
{
    internal const string ShotsToken = "{shots}";
    internal const string SizeToken = "{size}";
    private const int MaxTimeoutSeconds = 3600;

    internal static ResolvedDesktop? Resolve(DesktopTargets? desktop, Problems problems)
    {
        if (desktop is null)
        {
            return null;
        }

        problems.AddIf(!IsRepoRelative(desktop.Manifest), "$.desktop.manifest: must be a file path below the repo");
        problems.AddIf(string.IsNullOrEmpty(desktop.HomeEnv) || !EnvName.IsMatch(desktop.HomeEnv), "$.desktop.homeEnv: must be an UPPER_SNAKE environment variable name");
        CheckCommand(desktop.Fixture?.Command, "$.desktop.fixture.command", problems);
        CheckUp(desktop.Fixture?.Up, problems);
        problems.AddIf(desktop.Fixture?.Down is { } down && down.Any(string.IsNullOrEmpty), "$.desktop.fixture.down: an argument must not be empty");
        CheckCommand(desktop.Capture?.Command, "$.desktop.capture.command", problems);
        problems.AddIf(desktop.Capture?.Command is { } capture && !capture.Contains(ShotsToken, StringComparer.Ordinal), $"$.desktop.capture.command: must hold the element {ShotsToken}");
        problems.AddIf(desktop.Capture?.Command is { } sized && !sized.Any(static a => a.Contains(SizeToken, StringComparison.Ordinal)), $"$.desktop.capture.command: must hold {SizeToken}");
        problems.AddIf(desktop.Threshold is < 0 or > 100, "$.desktop.threshold: must be 0 to 100");
        problems.AddIf(desktop.TimeoutSeconds is < 1 or > MaxTimeoutSeconds, "$.desktop.timeoutSeconds: must be 1 to 3600");
        foreach (var name in (desktop.Env ?? new Dictionary<string, string>(StringComparer.Ordinal)).Keys)
        {
            problems.AddIf(!EnvName.IsMatch(name), $"$.desktop.env.{name}: the name must be UPPER_SNAKE");
        }

        var sizes = new List<ViewportSize>();
        foreach (var text in desktop.Sizes ?? DesktopTargets.DefaultSizes)
        {
            if (SizeText.TryParse(text, out var size))
            {
                sizes.Add(size);
            }
            else
            {
                problems.Add($"$.desktop.sizes: '{text}' is not a size like 1440x900");
            }
        }

        problems.AddIf(sizes.Count == 0, "$.desktop.sizes: must name at least one size");
        return new ResolvedDesktop(
            desktop.Manifest ?? string.Empty, desktop.HomeEnv ?? string.Empty,
            desktop.Fixture ?? new DesktopFixture([], []), desktop.Capture ?? new DesktopCapture([]), sizes,
            desktop.Threshold ?? DesktopTargets.DefaultThreshold, TimeSpan.FromSeconds(desktop.TimeoutSeconds ?? DesktopTargets.DefaultTimeoutSeconds),
            desktop.Env ?? new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <summary>True for a path that is relative, names a file and never leaves the directory it is relative to.</summary>
    internal static bool IsRepoRelative(string? path) =>
        !string.IsNullOrWhiteSpace(path) && !path.StartsWith('/') && !path.EndsWith('/') && path.Split('/').All(static s => s is not ("" or "." or ".."));

    private static void CheckCommand(IReadOnlyList<string>? command, string at, Problems problems) =>
        problems.AddIf(command is null || command.Count == 0 || command.Any(string.IsNullOrEmpty), $"{at}: must list a program and its arguments, none empty");

    private static void CheckUp(IReadOnlyList<IReadOnlyList<string>>? up, Problems problems)
    {
        problems.AddIf(up is null || up.Count == 0, "$.desktop.fixture.up: must list at least one step");
        problems.AddIf(up is not null && up.Any(static step => step is null || step.Count == 0 || step.Any(string.IsNullOrEmpty)), "$.desktop.fixture.up: each step must list its arguments, none empty");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex EnvName { get; }
}
