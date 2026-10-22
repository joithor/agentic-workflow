using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The code lens follows the selection (S-9): a chosen file reads its own callers and callees, anything else hides the panels.</summary>
internal sealed partial class ContextPageViewModel
{
    private async Task ShowCode(ContextMapViewModel map, string key, CancellationToken cancellationToken)
    {
        if (map.Node(key)?.Layout.Band != MapBand.Code)
        {
            Code.Clear();
            return;
        }

        var generation = Code.Begin();
        if (!await ReadCode(generation, [key], map.Draft, cancellationToken).ConfigureAwait(true) && Code.IsCurrent(generation))
        {
            Code.Fail();
        }
    }
}
