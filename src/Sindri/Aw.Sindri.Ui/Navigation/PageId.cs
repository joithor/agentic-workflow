namespace Aw.Sindri.Ui.Navigation;

/// <summary>The app's pages (UI spec §3.3). Agents, Repos, Today and Automatic work are "not served yet" and have none.</summary>
internal enum PageId
{
    /// <summary>Needs you, Running and With you, Queued, and the health strip (the default).</summary>
    Home,

    /// <summary>All five queue groups.</summary>
    Queue,

    /// <summary>Decisions and requests.</summary>
    Decisions,

    /// <summary>Services and jobs.</summary>
    Services,

    /// <summary>The control plane's health (S1b's status card in U0).</summary>
    Health,

    /// <summary>Local preferences only (⌘,).</summary>
    Settings,
}
