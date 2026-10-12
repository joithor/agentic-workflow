namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// Port: the running control plane's per-start human token (auth/human.token under AW_SINDRI_HOME on a desktop, API spec
/// §6), which the app sends as X-Sindri-Token with X-Sindri-Caller: human. The app reads it at each call, like the CLI,
/// so a restarted control plane's new token is picked up with no app restart. A head adapts it to its platform.
/// </summary>
public interface IHumanTokenSource
{
    /// <summary>Where the token is read from, for the log (a path on a desktop). Never the token itself.</summary>
    public string Location { get; }

    /// <summary>Reads the token as it is now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The token without surrounding whitespace, or null when there is none (no file, or an empty one).</returns>
    /// <exception cref="IOException">The platform can't read it.</exception>
    /// <exception cref="UnauthorizedAccessException">The platform may not read it.</exception>
    public ValueTask<string?> Read(CancellationToken cancellationToken);
}
