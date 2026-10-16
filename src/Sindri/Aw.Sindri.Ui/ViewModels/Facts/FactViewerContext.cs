using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>What a viewer needs from the frame: the API and where it is, the instance whose fact it shows, the clipboard and the clock, and the way to open an item.</summary>
/// <param name="Api">The API client.</param>
/// <param name="Clipboard">Where Copy puts text.</param>
/// <param name="Clock">The clock for "Copied".</param>
/// <param name="ApiUrl">Where the API is now; null while it is not known.</param>
/// <param name="InstanceId">The opaque id of the instance whose fact is shown.</param>
/// <param name="OpenItem">Opens an item's sheet.</param>
internal sealed record FactViewerContext(ISindriApi Api, IClipboard Clipboard, TimeProvider Clock, Func<Uri?> ApiUrl, string InstanceId, Action<string> OpenItem)
{
    /// <summary>Opens a link or a file in the system's apps; opens nothing until the head hands one over.</summary>
    public ILauncher Launcher { get; init; } = NullLauncher.Instance;
}
