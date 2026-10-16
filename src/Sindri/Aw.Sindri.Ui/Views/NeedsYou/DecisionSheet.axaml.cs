using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Views.NeedsYou;

/// <summary>
/// The decision sheet: a press on an option asks the view model to send Decide with it. The option buttons are rebuilt when
/// the options change (sending, a refusal), so focus goes back to the button of the option that was pressed: it is never lost.
/// </summary>
internal sealed partial class DecisionSheet : UserControl
{
    private string? _pressed;

    public DecisionSheet()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is DecisionSheetViewModel sheet)
            {
                sheet.PropertyChanged += (_, e) =>
                {
                    if (_pressed is not null && string.Equals(e.PropertyName, nameof(DecisionSheetViewModel.Options), StringComparison.Ordinal))
                    {
                        Dispatcher.UIThread.Post(RestoreFocus, DispatcherPriority.Loaded);
                    }
                };
            }
        };
    }

    private void OnOption(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DecisionSheetViewModel sheet && sender is Control { DataContext: DecisionOption option })
        {
            _pressed = option.Id;
            _ = sheet.Choose(option.Id);
        }
    }

    private void RestoreFocus()
    {
        var button = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyEnabled && b.DataContext is DecisionOption o && string.Equals(o.Id, _pressed, StringComparison.Ordinal));
        if (button is not null && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Button { DataContext: DecisionOption })
        {
            _ = button.Focus(NavigationMethod.Unspecified);
        }
    }
}
