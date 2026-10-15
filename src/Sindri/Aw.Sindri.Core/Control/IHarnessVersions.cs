namespace Aw.Sindri.Core.Control;

/// <summary>
/// The installed version of an agent harness's CLI (port, engine spec §6.2): what 'sindri doctor' compares with the
/// pinned harness probe.
/// </summary>
public interface IHarnessVersions
{
    /// <summary>
    /// Runs '<paramref name="command"/> --version' and returns the first version number it prints ('2.1.296'), or null when
    /// the command isn't installed, fails, times out or prints no version. Never an error: a missing harness has no version.
    /// </summary>
    /// <param name="command">The harness's command, e.g. 'claude', 'codex' or 'cursor-agent'.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The version, or null.</returns>
    public ValueTask<string?> Read(string command, CancellationToken cancellationToken);
}
