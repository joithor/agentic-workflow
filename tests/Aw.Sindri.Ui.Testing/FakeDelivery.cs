namespace Aw.Sindri.Ui.Testing;

/// <summary>A message in the fake channel and whether the holder handled it.</summary>
/// <param name="Message">The message's text.</param>
/// <param name="Handled">Set once the holder's reader handled it.</param>
internal sealed record FakeDelivery(string Message, TaskCompletionSource<bool> Handled);
