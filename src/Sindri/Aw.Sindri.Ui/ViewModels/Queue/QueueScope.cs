namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>Which rows J / K walk (UI spec §3.3, §8): those of the page the key was pressed on.</summary>
internal enum QueueScope
{
    /// <summary>The Queue page: the expanded, visible groups' rows, in the API's order.</summary>
    Page,

    /// <summary>Home: the Running and With you panel's rows, then the Queued panel's, as drawn.</summary>
    Home,
}
