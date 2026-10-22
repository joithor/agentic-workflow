using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The code lens follows the selection (S-9): a chosen file reads the callers and callees of the map's starting points, anything else hides the panels.</summary>
internal sealed partial class ContextPageViewModel
{
    private const int MaxCodeAnchors = 20;

    private async Task ShowCode(ContextMapViewModel map, string key, CancellationToken cancellationToken)
    {
        var anchors = map.Anchors.Select(a => a.Key).Take(MaxCodeAnchors).ToList();
        if (map.Node(key)?.Layout.Band != MapBand.Code || anchors.Count == 0
            || !await ReadCode(anchors, map.Draft, cancellationToken).ConfigureAwait(true))
        {
            Code.Clear();
        }
    }
}
