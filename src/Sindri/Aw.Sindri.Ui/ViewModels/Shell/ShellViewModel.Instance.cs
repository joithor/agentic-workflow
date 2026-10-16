using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The instance frame in the shell (<see cref="ShellViewModel"/>, continued; U8 T4): a workflow link opens it in the sheet, read by id when a row, the strip or a child pointer knew the id.</summary>
internal sealed partial class ShellViewModel
{
    private readonly Dictionary<string, string> _hintedIds = new(StringComparer.Ordinal);

    /// <summary>The instance frame.</summary>
    public InstanceFrameViewModel Instance { get; }

    /// <summary>True when the sheet's subject is a workflow instance (its own view in the sheet's content).</summary>
    public bool IsInstanceSheet => SheetLink?.Kind == DeepLinkKind.Workflow;

    /// <summary>The sheet's width as it is shown: an instance's is the widest, every other sheet keeps the human's.</summary>
    public double ShownSheetWidth => IsInstanceSheet ? UiPreferences.MaxSheetWidth : SheetWidth;

    /// <summary>The Workflows row the frame was opened from (its address), where Esc returns focus; null when it came from elsewhere.</summary>
    internal string? OpenerWorkflow { get; private set; }

    /// <summary>Opens an instance's frame.</summary>
    /// <param name="address">The address.</param>
    /// <param name="id">Its id when the caller knows it (so the read needs no lookup); null otherwise.</param>
    internal void OpenInstance(string address, string? id)
    {
        if (id is not null)
        {
            _hintedIds[address] = id;
        }

        Navigate(new DeepLink(DeepLinkKind.Workflow, address));
    }

    // Everything the frame needs once it exists (ShellViewModel's constructor calls this one line).
    private void InitInstance()
    {
        Health.Follow((url, token) => IsInstanceSheet ? Instance.Refresh(url, token) : Task.CompletedTask);
        Instance.OpenRequested += (_, e) => OpenInstance(e.Address, e.Id);
        Instance.ItemRequested += (_, e) => Navigate(new DeepLink(DeepLinkKind.Item, e.ItemId));
    }

    // The instance part of opening the sheet (OnSheetOpened): the frame names its instance at once and reads it; the id is the one a row or
    // the strip showed, else the list resolves the address.
    private void OnInstanceSheetOpened(DeepLink link)
    {
        OpenerWorkflow = null;
        Changed(nameof(IsInstanceSheet), nameof(ShownSheetWidth));
        if (link.Kind != DeepLinkKind.Workflow || link.Subject is not { } address)
        {
            return;
        }

        var id = KnownId(address);
        Instance.Show(address, id);
        if (Health.ApiUrl() is { } url)
        {
            _ = Instance.Open(url, address, id, CancellationToken.None);
        }
    }

    private string? KnownId(string address) =>
        _hintedIds.TryGetValue(address, out var hinted) ? hinted
        : Workflows.Groups.SelectMany(g => g.Rows).FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal))?.InstanceId
            ?? Strip.Rows.FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal))?.InstanceId;
}
