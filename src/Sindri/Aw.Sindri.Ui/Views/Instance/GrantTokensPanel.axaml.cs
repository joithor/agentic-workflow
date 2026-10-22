using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Views.Instance;

/// <summary>The Grant tokens panel, on the shared <see cref="Controls.HoldButton"/> and <see cref="Controls.TypedConfirmBox"/>: a complete hold or an exact typed phrase answers.</summary>
internal sealed partial class GrantTokensPanel : UserControl
{
    public GrantTokensPanel()
    {
        InitializeComponent();
        GrantHoldControl.Held += (_, e) => _ = (DataContext as GrantTokensViewModel)?.Held(e.HeldMs);
        GrantTypedBox.Submitted += (_, _) => _ = (DataContext as GrantTokensViewModel)?.SubmitTyped();
        GrantAmount.LostFocus += OnAmountLeft;
        GrantAmount.KeyDown += OnAmountKey;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is GrantTokensViewModel { IsReady: true } model)
        {
            _ = model.Prepare();   // the challenge for the suggested amount is there before the person holds
        }
    }

    // The challenge is bound to the amount: leaving the field (or Enter) asks for the one that fits what is written.
    private void OnAmountLeft(object? sender, RoutedEventArgs e) => _ = (DataContext as GrantTokensViewModel)?.Prepare();

    private void OnAmountKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = (DataContext as GrantTokensViewModel)?.Prepare();
        }
    }
}
