using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Views.Context;

/// <summary>The Context page (U9a owns this class): the head, the lens stack and the side column.</summary>
internal sealed partial class ContextPage : UserControl
{
    public ContextPage()
    {
        InitializeComponent();
    }

    // Enter in the anchor box reads the lenses for what is typed.
    private void OnAnchorKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ContextPageViewModel page)
        {
            e.Handled = true;
            _ = page.Submit();
        }
    }

    private void OnPick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string anchor } && DataContext is ContextPageViewModel page)
        {
            _ = page.Pick(anchor);
        }
    }
}
