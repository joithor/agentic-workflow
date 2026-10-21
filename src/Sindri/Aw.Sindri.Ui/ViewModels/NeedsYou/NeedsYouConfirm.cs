using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>NOT BUILT YET (red stub): the confirmations the inbox's action bar hosts.</summary>
internal sealed class NeedsYouConfirm : ObservableObject
{
    internal NeedsYouConfirm(CatalogViewModel catalog, Func<Uri?> url, ISindriApi api, TimeProvider clock, Func<bool>? reducedMotion = null)
    {
        TypedBox = new TypedConfirmBoxViewModel("ApproveProfile", catalog, clock);
        All = new ApproveAllViewModel(catalog, url, api, clock, reducedMotion);
    }

    internal event EventHandler? Changed;

    internal TypedConfirmBoxViewModel TypedBox { get; }

    internal ApproveAllViewModel All { get; }

    internal HoldButtonViewModel Hold => All.Hold;

    internal NeedsYouConfirmKind Kind { get; private set; }

    internal string Refusal { get; private set; } = "";

    internal bool Done { get; private set; }

    internal static bool IsApproval(NeedsYouEntry entry) => AppConfirm.Actions.Contains(entry.PrimaryAction, StringComparer.Ordinal);

    internal Task Show(NeedsYouRowViewModel? row)
    {
        _ = row;
        Changed?.Invoke(this, EventArgs.Empty);
        throw new NotSupportedException();
    }

    internal Task SubmitTyped(string typed) => throw new NotSupportedException(typed);

    internal Task Held(int heldMs) => throw new NotSupportedException(heldMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
