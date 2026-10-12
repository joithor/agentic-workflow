using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// The agent-variable probe (engine spec §6.2): takes a <see cref="HarnessProbeReport"/> in this process. Names only,
/// never a value, so no secret is recorded.
/// </summary>
internal static class HarnessProbe
{
    /// <summary>How many ancestors the probe reads: enough to reach launchd or init.</summary>
    internal const int Depth = 64;

    /// <summary>Probes this process.</summary>
    /// <param name="input">What was read from it.</param>
    /// <param name="versions">Reads a harness's version when its executable path doesn't carry one.</param>
    /// <param name="cancellationToken">Cancels the version read.</param>
    /// <returns>The report.</returns>
    internal static async ValueTask<HarnessProbeReport> Take(ProbeInput input, IHarnessVersions versions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(versions);
        List<ProbedProcess> ancestry = [.. input.Ancestry.Select(a => new ProbedProcess(HarnessProcess.BaseName(a.Name), Tilde(a.Executable, input.Home)))];
        var harness = input.Harness ?? PinnedProbe.Recognise(input.Variables, ancestry);
        var pinned = PinnedProbe.All.FirstOrDefault(h => !h.Human && (string.Equals(h.Harness, harness, StringComparison.Ordinal) || string.Equals(h.Family, harness, StringComparison.Ordinal)));
        var version = FromExecutable(ancestry, pinned?.Processes ?? PinnedProbe.Processes)
            ?? (pinned?.VersionCommand is { } command ? await versions.Read(command, cancellationToken).ConfigureAwait(false) : null);
        return new HarnessProbeReport(harness, [.. input.Variables.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)], ancestry, input.Terminal, version);
    }

    /// <summary>The version a harness ancestor's executable path carries ('…/claude/versions/2.1.296'), or null.</summary>
    private static string? FromExecutable(IEnumerable<ProbedProcess> ancestry, IReadOnlyList<HarnessProcess> harness) =>
        ancestry.Where(a => harness.Any(h => h.Matches(a)))
            .Select(a => a.Executable is { } path ? HarnessProcess.BaseName(path) : string.Empty)
            .FirstOrDefault(IsVersion);

    private static bool IsVersion(string text) =>
        text.Length > 2 && char.IsAsciiDigit(text[0]) && char.IsAsciiDigit(text[^1]) && text.Contains('.', StringComparison.Ordinal)
        && text.All(c => char.IsAsciiDigit(c) || c == '.');

    /// <summary>Writes the home directory '~', so a golden holds no user name.</summary>
    private static string? Tilde(string? path, string? home) =>
        path is not null && home is { Length: > 1 } && path.StartsWith(home.TrimEnd('/') + "/", StringComparison.Ordinal)
            ? "~" + path[home.TrimEnd('/').Length..]
            : path;
}
