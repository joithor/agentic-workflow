namespace Aw.BugFix.Features.Init;

/// <summary>The Init: start a bug fix at intake from the ticket file.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="TicketFile">The ticket.json path, relative to the working directory or full.</param>
public sealed record InitRequest(
    string StateDirectory,
    string TicketFile);
