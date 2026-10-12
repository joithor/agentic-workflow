namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// Port: the control plane's per-start human token (API spec §6: '&lt;home&gt;/auth/human.token', minted by each
/// 'sindri serve'). The app sends it as X-Sindri-Token on every request; without it a human call is refused with
/// SND-AUTH-001. Read before each request, so a restarted control plane's new token is picked up.
/// </summary>
public interface IHumanTokenSource
{
    /// <summary>Where the token is read from, for messages (a path on a desktop).</summary>
    public string Location { get; }

    /// <summary>Reads the token.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The token text without surrounding whitespace, or null when there is none.</returns>
    public ValueTask<string?> Read(CancellationToken cancellationToken);
}
