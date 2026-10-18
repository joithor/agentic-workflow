namespace Aw.Sindri.Core.Onyx;

/// <summary>Something about the managed Onyx the human has to act on, for the needs-you list.</summary>
/// <param name="Title">One line.</param>
/// <param name="Reason">Why, with no secret in it.</param>
/// <param name="Command">The command to run.</param>
public sealed record OnyxNotice(string Title, string Reason, string Command);
