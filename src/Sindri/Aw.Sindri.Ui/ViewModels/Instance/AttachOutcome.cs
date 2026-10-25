namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>What an Attach came to, in words.</summary>
/// <param name="Line">The sentence.</param>
/// <param name="Command">The attach command to show with a Copy (the print viewer), or empty.</param>
/// <param name="Refused">True when the server refused or the send failed.</param>
internal sealed record AttachOutcome(string Line, string Command, bool Refused);
