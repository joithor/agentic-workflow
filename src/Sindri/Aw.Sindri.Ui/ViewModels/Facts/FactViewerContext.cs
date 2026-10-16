#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
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
internal sealed record FactViewerContext(ISindriApi Api, IClipboard Clipboard, TimeProvider Clock, Func<Uri?> ApiUrl, string InstanceId, Action<string> OpenItem);
