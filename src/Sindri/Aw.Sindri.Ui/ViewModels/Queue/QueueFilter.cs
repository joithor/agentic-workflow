namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>Which of the API's groups the Queue page shows (UI spec §3.3: G Q all, G R running, G W with you).</summary>
internal enum QueueFilter
{
    /// <summary>Every group.</summary>
    All,

    /// <summary>Only the API's 'running' group.</summary>
    Running,

    /// <summary>Only the API's 'withYou' group.</summary>
    WithYou,
}
