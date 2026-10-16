namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>Where focus lands when the frame opens: always a read action, never a command that changes the instance (§11.2).</summary>
internal enum FrameFocus
{
    /// <summary>The subject's Open in link, when the subject has a link.</summary>
    OpenIn,

    /// <summary>Copy status.</summary>
    CopyStatus,

    /// <summary>Back, when there is nothing else to read.</summary>
    Back,
}
