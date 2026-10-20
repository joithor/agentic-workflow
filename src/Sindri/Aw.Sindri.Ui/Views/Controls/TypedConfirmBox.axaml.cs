using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The typed confirmation: the field and the button press the same thing. It reports the typed text through <see cref="Submitted"/> only when it
/// matches the served phrase exactly, and sends nothing itself.
/// </summary>
internal sealed partial class TypedConfirmBox : UserControl
{
    public TypedConfirmBox()
    {
        InitializeComponent();
        TypedField.KeyDown += OnKey;
    }

    /// <summary>Raised when the person presses the button, or Enter in the field, on the exact phrase; the argument is the typed text.</summary>
    internal event EventHandler<SubmittedEventArgs>? Submitted;

    /// <summary>The names of the field and its button are this prefix and the prefix plus "Button", so a host's tests and focus code can find them.</summary>
    public string PartPrefix
    {
        get => TypedField.Name ?? string.Empty;
        set
        {
            TypedField.Name = value;
            TypedButton.Name = value + "Button";
        }
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Press();
            e.Handled = true;
        }
    }

    private void OnPress(object? sender, RoutedEventArgs e) => Press();

    private void Press()
    {
        if (DataContext is TypedConfirmBoxViewModel model && model.Submit() is { } typed)
        {
            Submitted?.Invoke(this, new SubmittedEventArgs(typed));
        }
    }
}
