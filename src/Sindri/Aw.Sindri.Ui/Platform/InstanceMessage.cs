namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// A message a later launch sent (<see cref="ISingleInstance.Messages"/>): its text, and whether its sender still waits
/// for it. The app acts on it only while <see cref="Withdrawn"/> isn't cancelled, checked again on the UI thread just
/// before acting, so a message whose wait ended (the sender was told the app isn't answering) is never acted on late.
/// </summary>
/// <param name="text"><see cref="InstanceMessages.Focus"/> or a deep link's text, unparsed.</param>
/// <param name="withdrawn">Cancelled once the running instance has answered the sender or stopped waiting for the handling.</param>
public sealed class InstanceMessage(string text, CancellationToken withdrawn)
{
    /// <summary>The message's text, unparsed.</summary>
    public string Text { get; } = text;

    /// <summary>Cancelled once the sender's wait has ended: too late to act on.</summary>
    public CancellationToken Withdrawn { get; } = withdrawn;

    /// <summary>Claims the message for acting on it.</summary>
    /// <returns>True for the one claim that wins while the sender still waits.</returns>
    public bool TryAct() => throw new NotSupportedException();
}
