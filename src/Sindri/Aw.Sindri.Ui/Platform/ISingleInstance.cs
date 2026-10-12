namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// Port: one running app per user (UI spec §18 U0). The first launch claims the instance and receives the messages later
/// launches send; a later launch sends <see cref="InstanceMessages.Focus"/> (or a deep link) and exits. A message only
/// navigates: the app parses it as a deep link and ignores anything else, and no message ever runs an action. The desktop
/// adapter is a local channel only the user can open (0600, under the app's temp-home-scoped state directory).
/// </summary>
public interface ISingleInstance : IAsyncDisposable
{
    /// <summary>Claims the instance for this process.</summary>
    /// <param name="cancellationToken">Cancels the claim.</param>
    /// <returns>True when this process is now the running instance; false when another one holds it.</returns>
    public ValueTask<bool> TryClaim(CancellationToken cancellationToken);

    /// <summary>Sends a message to the running instance (from a launch whose claim failed).</summary>
    /// <param name="message"><see cref="InstanceMessages.Focus"/> or a deep link's text.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>True once the running instance has handled it; false when none answers within 2 s (none runs, or it holds the claim but doesn't read its messages).</returns>
    public ValueTask<bool> Send(string message, CancellationToken cancellationToken);

    /// <summary>The messages later launches send, in order, while this process holds the claim.</summary>
    /// <param name="cancellationToken">Ends the stream.</param>
    /// <returns>Each message's text, unparsed.</returns>
    public IAsyncEnumerable<string> Messages(CancellationToken cancellationToken);

    /// <summary>The process id of the instance that holds the claim, when one does and it is known.</summary>
    /// <returns>The id, or null.</returns>
    public int? HolderProcessId();
}
