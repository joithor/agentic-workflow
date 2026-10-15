namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// A message a later launch sent (<see cref="ISingleInstance.Messages"/>): its text, and the one decision whether the app
/// acts on it. The app calls <see cref="TryAct"/> on the UI thread just before acting and acts only when it returns
/// true; that same claim is the sender's answer, so a message the app acts on is never answered "not handled", and one
/// whose sender's wait ended first is never acted on late.
/// </summary>
/// <param name="text"><see cref="InstanceMessages.Focus"/> or a deep link's text, unparsed.</param>
/// <param name="tryAct">Claims the message: true for the first claim while the sender still waits, false once withdrawn or claimed.</param>
/// <param name="withdrawn">Cancelled once the running instance has answered the sender or stopped waiting for the handling.</param>
public sealed class InstanceMessage(string text, Func<bool> tryAct, CancellationToken withdrawn)
{
    /// <summary>The message's text, unparsed.</summary>
    public string Text { get; } = text;

    /// <summary>Cancelled once the sender's wait has ended (answered or given up): too late to act on.</summary>
    public CancellationToken Withdrawn { get; } = withdrawn;

    /// <summary>Claims the message for acting on it; the sender is then told it was handled.</summary>
    /// <returns>True for the one claim that wins while the sender still waits; false once withdrawn or already claimed.</returns>
    public bool TryAct() => tryAct();
}
