using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>'sindri doctor --probe' and the harness lines plain 'sindri doctor' adds (engine spec §6.2). Neither calls the control plane.</summary>
/// <param name="ancestry">This process's ancestry.</param>
/// <param name="versions">Reads installed harness versions.</param>
/// <param name="environment">Reads an environment variable (the home directory).</param>
/// <param name="interactive">Whether stdin and stdout are a terminal.</param>
internal sealed class ProbeCommands(IProcessAncestry ancestry, IHarnessVersions versions, Func<string, string?> environment, bool interactive)
{
    /// <summary>Prints this process's probe as JSON. Exit 0.</summary>
    /// <param name="harness">The harness name the owner gave, or null.</param>
    /// <param name="output">Where it prints.</param>
    /// <param name="cancellationToken">Cancels the version read.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> Probe(string? harness, Output output, CancellationToken cancellationToken) =>
        await output.Json(await Live(harness, cancellationToken).ConfigureAwait(false), 0).ConfigureAwait(false);

    /// <summary>The doctor's harness lines against the pinned probe.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The checks.</returns>
    internal async ValueTask<IReadOnlyList<DoctorCheck>> DoctorChecks(CancellationToken cancellationToken) =>
        await HarnessDoctor.Checks(PinnedProbe.All, await Live(harness: null, cancellationToken).ConfigureAwait(false), versions, cancellationToken).ConfigureAwait(false);

    private ValueTask<HarnessProbeReport> Live(string? harness, CancellationToken cancellationToken) =>
        HarnessProbe.Take(
            new ProbeInput(harness, [.. Environment.GetEnvironmentVariables().Keys.Cast<string>()], environment("HOME"),
                ancestry.Ancestors(Environment.ProcessId, HarnessProbe.Depth), interactive),
            versions, cancellationToken);
}
