namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The tmux verbs Sindri runs (ARCH-SESS-001): list and display only. Adding one is a reviewed change to this enum, its name below and the architecture test's list.</summary>
internal enum TmuxVerb
{
    ListSessions,
    ListPanes,
    ListClients,
    DisplayMessage,
    CapturePane,
}
