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

    /// <summary>Open root instances grouped by recipe, and the instance frame (screens spec §4.2).</summary>
    Workflows,

    /// <summary>Items (screens spec §3, chord G Q).</summary>
    Work,

    /// <summary>Recipes (screens spec §4.3).</summary>
    Recipes,

    /// <summary>The context lens stack (screens spec §4.4).</summary>
    Context,

    /// <summary>Live agent sessions (screens spec §4.5).</summary>
    Agents,

    /// <summary>Dependencies, services and health (screens spec §4.6).</summary>
    System,

    /// <summary>Local preferences only (⌘,).</summary>
    Settings,
}
