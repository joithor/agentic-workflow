using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Item;

/// <summary>The Create item form; Enter in the title sends, Esc and Cancel close it with nothing sent.</summary>
internal sealed partial class CreateItemDialog : UserControl
{
    public CreateItemDialog()
    {
        InitializeComponent();
    }

    /// <summary>The app's copy for the form.</summary>
    public ItemText Text { get; } = ItemText.Instance;

    /// <summary>Moves focus into the title field (the form just opened).</summary>
    internal void FocusTitle() => this.Get<TextBox>("TitleField").Focus(NavigationMethod.Tab);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is CreateItemViewModel form && e.Key == Key.Enter && e.Source is TextBox { AcceptsReturn: false })
        {
            _ = form.Submit(CancellationToken.None);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => (DataContext as CreateItemViewModel)?.Close();

    private void OnScrim(object? sender, PointerPressedEventArgs e) => (DataContext as CreateItemViewModel)?.Close();
}
