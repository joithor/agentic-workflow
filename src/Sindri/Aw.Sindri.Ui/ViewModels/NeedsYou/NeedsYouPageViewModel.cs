using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The Needs-you inbox page (F3 needs-you spec §2): a grouped list of the rows, and one request card for the selected one.</summary>
internal sealed class NeedsYouPageViewModel : ObservableObject
{
    internal NeedsYouPageViewModel(NeedsYouViewModel list, TimeProvider clock, IClipboard clipboard)
    {
        _ = (list, clock, clipboard);
    }

    /// <summary>The groups, in the page's order.</summary>
    public IReadOnlyList<NeedsYouInboxGroup> Groups => throw new NotSupportedException();

    /// <summary>The selected row; null for none.</summary>
    internal NeedsYouRowViewModel? Selected => throw new NotSupportedException();

    /// <summary>The header's count: "3 waiting, 1 runs".</summary>
    public string CountText => throw new NotSupportedException();

    /// <summary>The card's kind word, at the right of its head.</summary>
    public string KindWord => throw new NotSupportedException();

    /// <summary>The card's footer, left: what the row waits for.</summary>
    public string FooterLeft => throw new NotSupportedException();

    /// <summary>The card's footer, right: how long it has waited.</summary>
    public string FooterRight => throw new NotSupportedException();

    /// <summary>Selects the first row of the first group when none is selected (or the selected one is gone).</summary>
    internal void SelectFirst() => throw new NotSupportedException();

    /// <summary>Moves the selection one row through the groups in the page's order.</summary>
    /// <param name="step">1 for the next row, -1 for the previous.</param>
    /// <returns>True when a row is selected after the move.</returns>
    internal bool Move(int step) => throw new NotSupportedException();
}
