using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Item;

namespace Aw.Sindri.Ui.Views.Item;

/// <summary>The Create item form; Enter in the title sends, Esc and Cancel close it with nothing sent.</summary>
internal sealed partial class CreateItemDialog : UserControl
{
    private CreateItemViewModel? _form;

    public CreateItemDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_form is { } old)
        {
            old.PropertyChanged -= OnForm;
        }

        _form = DataContext as CreateItemViewModel;
        if (_form is { } form)
        {
            form.PropertyChanged += OnForm;
        }

        base.OnDataContextChanged(e);
    }

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

    // A blank submit asks for focus on the title, wherever the press came from (the button, Enter, the palette).
    private void OnForm(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(CreateItemViewModel.TitleFocusRequests), StringComparison.Ordinal))
        {
            FocusTitle();
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => (DataContext as CreateItemViewModel)?.Close();

    private void OnScrim(object? sender, PointerPressedEventArgs e) => (DataContext as CreateItemViewModel)?.Close();
}
