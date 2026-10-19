namespace Aw.Sindri.Ui.Platform;

/// <summary>The adapters that belong to the window and so reach the OS through it: where a link or a file opens, and where a file is chosen. One value, so the shell's constructor takes them as one port.</summary>
/// <param name="Launcher">Opens links and files in the system's apps.</param>
/// <param name="Files">Chooses a file for a start input.</param>
internal sealed record WindowPorts(ILauncher Launcher, IFilePicker Files)
{
    /// <summary>A head with neither: nothing opens and no file is ever chosen.</summary>
    internal static WindowPorts None { get; } = new(NullLauncher.Instance, NullFilePicker.Instance);
}
