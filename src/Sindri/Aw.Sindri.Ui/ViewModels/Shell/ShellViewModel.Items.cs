using System.ComponentModel;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's queue and item parts (<see cref="ShellViewModel"/>, continued; UI spec §4.5, §4.6, U2).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The queue (U2): the Queue page and Home's Running and Queued panels.</summary>
    public QueueViewModel Queue { get; }

    /// <summary>The item detail sheet (U2), shown when the sheet's subject is an item.</summary>
    public ItemSheetViewModel Item { get; }

    /// <summary>The Create item form (U2, `C`).</summary>
    public CreateItemViewModel Create { get; }

    /// <summary>True when the sheet's subject is an item (its own view), else the sheet's placeholder body.</summary>
    public bool IsItemSheet { get; private set; }

    /// <summary>Opens the Queue page on one filter (G Q, G R, G W; Home's "Show all N in Queue").</summary>
    /// <param name="filter">The filter.</param>
    internal void GoToQueue(QueueFilter filter)
    {
        Queue.Filter(filter);
        GoTo(PageId.Queue);
    }

    /// <summary>Opens an item's sheet (a row's Enter or title click, §3.2).</summary>
    /// <param name="itemId">The item's id.</param>
    internal void OpenItem(string itemId)
    {
        Navigate(new DeepLink(DeepLinkKind.Item, itemId));
        OpenerRow = itemId;
    }

    /// <summary>The row the sheet was opened from (its item id), where Esc returns focus; null when the sheet came from elsewhere.</summary>
    internal string? OpenerRow { get; private set; }

    private static NavEntry[] Nav(string queued) =>
    [
        new(PageId.Home, Labels.Home, "H", string.Empty),
        new(PageId.Queue, Labels.Queue, "Q", queued),
        new(PageId.Decisions, Labels.Decisions, "D", string.Empty),
        new(PageId.Services, Labels.Services, "S", string.Empty),
    ];

    // §3.2: the Queue tab carries the queue's count from the view's rows.
    private void OnQueueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(QueueViewModel.Total) or nameof(QueueViewModel.IsLoading)))
        {
            return;
        }

        var count = Queue.IsLoading ? string.Empty : Queue.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (Navigation[1].Count != count)
        {
            Navigation = Nav(count);
            Changed(nameof(Navigation), nameof(SelectedNav));
        }
    }

    /// <summary>Goes where a deep link points: a page, or the detail sheet with its subject.</summary>
    /// <param name="link">The link.</param>
    internal void Navigate(DeepLink link)
    {
        if (link.Kind == DeepLinkKind.HomeNeedsYou || link.Subject is null)
        {
            GoTo(PageId.Home);
            return;
        }

        SheetSubject = link.Subject;
        OpenerRow = null;
        IsSheetOpen = true;
        IsItemSheet = link.Kind == DeepLinkKind.Item;
        Changed(nameof(SheetSubject), nameof(EdgeTabName), nameof(IsSheetOpen), nameof(HasEdgeTab), nameof(IsSheetShown), nameof(IsEdgeTabShown), nameof(IsItemSheet));
        if (!IsItemSheet)
        {
            return;
        }

        Item.Show(link.Subject);
        if (Health.ApiUrl() is { } url)
        {
            _ = Item.Open(url, link.Subject, CancellationToken.None);
        }
    }

    /// <summary>Closes the sheet (Esc); its subject stays on the edge tab.</summary>
    internal void CloseSheet()
    {
        IsSheetOpen = false;
        Changed(nameof(IsSheetOpen), nameof(IsSheetShown), nameof(IsEdgeTabShown));
    }

    /// <summary>Opens the sheet again on its last subject (the edge tab).</summary>
    internal void ReopenSheet()
    {
        if (!HasEdgeTab)
        {
            return;
        }

        (IsSheetOpen, OpenerRow) = (true, null);
        Changed(nameof(IsSheetOpen), nameof(IsSheetShown), nameof(IsEdgeTabShown));
    }
}
