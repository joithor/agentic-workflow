namespace Aw.Sindri.Core.Sessions;

/// <summary>One row of 'tmux list-clients'.</summary>
/// <param name="Pid">The client process.</param>
/// <param name="Tty">The client's terminal device.</param>
/// <param name="Session">The session it shows.</param>
public sealed record TmuxClient(int Pid, string Tty, string Session);
