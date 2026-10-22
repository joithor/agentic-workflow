using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Views.Setup;

#pragma warning disable MA0182 // why: the step row places it in XAML, which the analyzer can't see
/// <summary>The profile row's typed approval: it draws the view model and passes the exact typed phrase on.</summary>
internal sealed partial class SetupProfileConfirmView : UserControl
{
    public SetupProfileConfirmView()
    {
        InitializeComponent();
        SetupProfileTypedBox.Submitted += (_, e) => _ = (DataContext as SetupProfileConfirmViewModel)?.SubmitTyped(e.Text);
    }
}
#pragma warning restore MA0182
