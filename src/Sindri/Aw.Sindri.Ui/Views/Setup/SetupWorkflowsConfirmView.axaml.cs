using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Approvals;

namespace Aw.Sindri.Ui.Views.Setup;

#pragma warning disable MA0182 // why: the step row places it in XAML, which the analyzer can't see
/// <summary>The workflows row's plan, hold and typed alternative: a complete hold or the exact phrase answers; the view holds no other logic.</summary>
internal sealed partial class SetupWorkflowsConfirmView : UserControl
{
    public SetupWorkflowsConfirmView()
    {
        InitializeComponent();
        SetupWorkflowsHold.Held += (_, e) => _ = (DataContext as ApproveAllViewModel)?.Held(e.HeldMs);
        SetupWorkflowsTypedBox.Submitted += (_, _) => _ = (DataContext as ApproveAllViewModel)?.SubmitTyped();
    }

    private void OnRetry(object? sender, RoutedEventArgs e) => _ = (DataContext as ApproveAllViewModel)?.Retry();
}
#pragma warning restore MA0182
